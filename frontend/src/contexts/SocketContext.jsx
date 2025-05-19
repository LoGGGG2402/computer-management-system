/**
 * @fileoverview Socket Context Provider for managing WebSocket connections in the computer management system
 * 
 * This context provides real-time communication with the server through Socket.IO,
 * handling authentication, computer subscription management, and status updates.
 * It establishes and maintains the WebSocket connection, authenticates with the user token,
 * and provides methods to subscribe/unsubscribe from computer status updates.
 * 
 * @module SocketContext
 */
import React, { createContext, useContext, useEffect, useState, useCallback, useMemo, useRef } from 'react';
import io from 'socket.io-client';
import { useAuth } from './AuthContext';

const SocketContext = createContext(null);

/**
 * Socket Provider Component
 * 
 * Establishes and manages WebSocket connections with the backend server.
 * Handles authentication, reconnection, and maintains computer status information.
 * Provides methods for subscribing to and unsubscribing from computer status updates.
 * 
 * @component
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components
 * @returns {React.ReactElement} Socket provider component
 */
export const SocketProvider = ({ children }) => {
  const socketRef = useRef(null);
  const [computerStatuses, setComputerStatuses] = useState({});
  const [isSocketReady, setIsSocketReady] = useState(false);
  const { isAuthenticated, user, logoutAction } = useAuth();

  /**
   * Establishes and manages the WebSocket connection based on authentication state
   * 
   * When a user is authenticated:
   * 1. Creates a new Socket.IO connection with the backend
   * 2. Sets up authentication with the user token
   * 3. Sets up event listeners for computer status updates
   * 
   * When a user is not authenticated:
   * 1. Disconnects any existing socket connection
   * 2. Clears status data
   * 
   * @effect
   * @listens isAuthenticated - Authentication state from AuthContext
   * @listens user?.token - JWT token for authentication
   * @listens logoutAction - Logout function from AuthContext
   */
  useEffect(() => {
    if (isAuthenticated && user?.token) {
      if (!socketRef.current || !socketRef.current.connected) {
        if (socketRef.current) {
            socketRef.current.disconnect();
        }

        setIsSocketReady(false);
        setComputerStatuses({});

        const newSocket = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
          reconnection: true,
          reconnectionAttempts: 5,
          reconnectionDelay: 1000,
          extraHeaders: {
            'X-Client-Type': 'frontend'
          }
        });
        socketRef.current = newSocket;

        newSocket.on('connect', () => {
          newSocket.emit('frontend:authenticate', { token: user.token });
        });

        newSocket.on('auth_response', (data) => {
          if (data?.status === 'success') {
            setIsSocketReady(true);
          } else {
            setIsSocketReady(false);
            newSocket.disconnect();
          }
        });

        newSocket.on('disconnect', (reason) => {
          setIsSocketReady(false);
          setComputerStatuses({});
        });

        newSocket.on('connect_error', (err) => {
          setIsSocketReady(false);
        });

        newSocket.on('computer:status_updated', (data) => {
          if (data && data.computerId) {
             setComputerStatuses(prev => ({
               ...prev,
               [data.computerId]: {
                 status: data.status,
                 cpuUsage: data.cpuUsage,
                 ramUsage: data.ramUsage,
                 diskUsage: data.diskUsage,
                 timestamp: data.timestamp || Date.now()
               }
             }));
          }
        });

        newSocket.on('subscribe_response', (data) => {});

        newSocket.on('unsubscribe_response', (data) => {
           if (data.status === 'success' && data.computerId) {
                setComputerStatuses(prev => {
                    const newState = {...prev};
                    delete newState[data.computerId];
                    return newState;
                });
           }
        });

      }
    } else {
      if (socketRef.current?.connected) {
        socketRef.current.disconnect();
        socketRef.current = null;
        setIsSocketReady(false);
        setComputerStatuses({});
      }
    }

    return () => {
      if (socketRef.current) {
        socketRef.current.off('connect');
        socketRef.current.off('auth_response');
        socketRef.current.off('disconnect');
        socketRef.current.off('connect_error');
        socketRef.current.off('computer:status_updated');
        socketRef.current.off('subscribe_response');
        socketRef.current.off('unsubscribe_response');
        socketRef.current.disconnect();
        socketRef.current = null;
        setIsSocketReady(false);
        setComputerStatuses({});
      }
    };
  }, [isAuthenticated, user?.token, logoutAction]);

  /**
   * Subscribes to status updates for a specific computer
   * 
   * Sends a subscription request to the server for a specific computer's status.
   * The backend verifies the user has access to the computer before allowing the subscription.
   * After successful subscription, the server will send status updates to this client.
   * 
   * @function
   * @param {number} computerId - ID of the computer to subscribe to
   * @returns {void}
   */
  const subscribeToComputer = useCallback((computerId) => {
    if (isSocketReady && socketRef.current?.connected && computerId) {
       socketRef.current.emit('frontend:subscribe', { computerId });
    }
  }, [isSocketReady]);

  /**
   * Unsubscribes from status updates for a specific computer
   * 
   * Sends an unsubscribe request to the server to stop receiving
   * updates about a specific computer's status.
   * 
   * @function
   * @param {number} computerId - ID of the computer to unsubscribe from
   * @returns {void}
   */
  const unsubscribeFromComputer = useCallback((computerId) => {
    if (isSocketReady && socketRef.current?.connected && computerId) {
      socketRef.current.emit('frontend:unsubscribe', { computerId });
    }
  }, [isSocketReady]);

  /**
   * Gets the current status for a specific computer
   * 
   * Returns status information including online/offline status and system usage metrics.
   * If no status is available, returns default offline values.
   * 
   * @function
   * @param {number} computerId - ID of the computer
   * @returns {Object} Computer status object
   * @returns {string} return.status - Current status ('online' or 'offline')
   * @returns {number} return.cpuUsage - CPU usage percentage (0-100)
   * @returns {number} return.ramUsage - RAM usage percentage (0-100)
   * @returns {number} return.diskUsage - Disk usage percentage (0-100)
   * @returns {number} return.timestamp - Last update timestamp
   */
  const getComputerStatus = useCallback((computerId) => {
    return computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0, diskUsage: 0, timestamp: 0 };
  }, [computerStatuses]);

  const contextValue = useMemo(() => ({
    /**
     * Current socket.io instance
     * @type {import('socket.io-client').Socket|null}
     */
    socket: socketRef.current,
    
    /**
     * Whether the socket is authenticated and ready for use
     * @type {boolean}
     */
    isSocketReady,
    
    /**
     * Subscribe to a computer's status updates
     * @type {function(number): void}
     */
    subscribeToComputer,
    
    /**
     * Unsubscribe from a computer's status updates
     * @type {function(number): void}
     */
    unsubscribeFromComputer,
    
    /**
     * Get status information for a computer
     * @type {function(number): Object}
     */
    getComputerStatus,
  }), [isSocketReady, subscribeToComputer, unsubscribeFromComputer, getComputerStatus]);

  return (
    <SocketContext.Provider value={contextValue}>
      {children}
    </SocketContext.Provider>
  );
};

/**
 * Hook for accessing the socket context
 * 
 * @function
 * @returns {Object} Socket context value
 * @returns {import('socket.io-client').Socket|null} return.socket - Socket.IO instance
 * @returns {boolean} return.isSocketReady - Whether socket is authenticated and ready
 * @returns {function(number): void} return.subscribeToComputer - Subscribe to computer updates
 * @returns {function(number): void} return.unsubscribeFromComputer - Unsubscribe from computer updates
 * @returns {function(number): Object} return.getComputerStatus - Get status for a computer
 * @throws {Error} If used outside of a SocketProvider
 */
export const useSocket = () => {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error('useSocket must be used within a SocketProvider');
  }
  return context;
};
