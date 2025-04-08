import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import io from 'socket.io-client';
import { useAuth } from './AuthContext';

// Create context
const SocketContext = createContext(null);

// Create provider component
export const SocketProvider = ({ children }) => {
  const [socket, setSocket] = useState(null);
  const [connected, setConnected] = useState(false);
  const [computerStatuses, setComputerStatuses] = useState({});
  const { user, isAuthenticated } = useAuth();

  // Initialize socket
  useEffect(() => {
    if (!isAuthenticated || !user) {
      console.log('[SocketContext] Not connecting - User not authenticated:', { isAuthenticated, user });
      return;
    }

    console.log('[SocketContext] Initializing socket connection');
    const socketInstance = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
      autoConnect: false,
      reconnection: true,
      reconnectionAttempts: 5,
      reconnectionDelay: 1000,
      auth: { token: user.token }
    });

    // Set up event listeners
    socketInstance.on('connect', () => {
      console.log('[SocketContext] Socket connected, authenticating...');
      setConnected(true);
      
      // Authenticate with backend
      socketInstance.emit('frontend:authenticate', {
        token: user.token
      });
    });

    socketInstance.on('disconnect', () => {
      console.log('WebSocket disconnected');
      setConnected(false);
    });

    socketInstance.on('error', (error) => {
      console.error('WebSocket error:', error);
      setConnected(false);
    });

    socketInstance.on('auth_response', (response) => {
      console.log('[SocketContext] Authentication response:', response);
      if (response.status === 'success') {
        console.log('[SocketContext] Socket authentication successful');
      } else {
        console.error('[SocketContext] Socket authentication failed:', response.message);
      }
    });

    // Update computer status when received
    socketInstance.on('computer:status_updated', (data) => {
      console.log('[SocketContext] Received status update:', data);
      setComputerStatuses(prev => ({
        ...prev,
        [data.computerId]: {
          status: data.status,
          cpuUsage: data.cpuUsage,
          ramUsage: data.ramUsage,
          timestamp: data.timestamp
        }
      }));
    });

    // Connect to the server
    socketInstance.connect();
    setSocket(socketInstance);

    // Cleanup function
    return () => {
      if (socketInstance) {
        socketInstance.disconnect();
      }
    };
  }, [isAuthenticated, user]);

  // Subscribe to rooms (for computer status updates)
  const subscribeToRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    
    console.log('[SocketContext] Subscribing to rooms:', roomIds);
    socket.emit('frontend:subscribe', { roomIds });
  }, [socket, connected]);

  // Unsubscribe from rooms
  const unsubscribeFromRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    
    console.log('[SocketContext] Unsubscribing from rooms:', roomIds);
    socket.emit('frontend:unsubscribe', { roomIds });
  }, [socket, connected]);

  // Send a command to an agent
  const sendCommand = useCallback((computerId, command) => {
    if (!socket || !connected) {
      console.error('[SocketContext] Cannot send command: not connected');
      return Promise.reject(new Error('Not connected to the server'));
    }
    
    console.log('[SocketContext] Sending command to computer:', computerId, command);
    
    return new Promise((resolve, reject) => {
      socket.emit('frontend:send_command', { computerId, command }, (response) => {
        if (response && response.success) {
          console.log('[SocketContext] Command sent successfully:', response);
          resolve(response);
        } else {
          console.error('[SocketContext] Failed to send command:', response);
          reject(new Error(response?.message || 'Failed to send command'));
        }
      });
    });
  }, [socket, connected]);

  // Get real-time status for a specific computer
  const getComputerStatus = useCallback((computerId) => {
    return computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0 };
  }, [computerStatuses]);

  // Context value
  const contextValue = {
    socket,
    connected,
    computerStatuses,
    subscribeToRooms,
    unsubscribeFromRooms,
    sendCommand,
    getComputerStatus
  };

  return (
    <SocketContext.Provider value={contextValue}>
      {children}
    </SocketContext.Provider>
  );
};

// Custom hook to use the context
export const useSocket = () => {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error('useSocket must be used within a SocketProvider');
  }
  return context;
};

export default SocketContext;