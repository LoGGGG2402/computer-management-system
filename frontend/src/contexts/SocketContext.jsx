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
  const [authResponse, setAuthResponse] = useState(null);
  const { user, isAuthenticated } = useAuth();

  // Initialize socket
  useEffect(() => {
    if (!isAuthenticated || !user) return;

    const socketInstance = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
      autoConnect: false,
      reconnection: true,
      reconnectionAttempts: 5,
      reconnectionDelay: 1000
    });

    // Set up event listeners
    socketInstance.on('connect', () => {
      console.log('Socket connected!');
      setConnected(true);
      
      // Authenticate with backend
      socketInstance.emit('frontend:authenticate', {
        token: user.token
      });
    });

    socketInstance.on('disconnect', () => {
      console.log('Socket disconnected!');
      setConnected(false);
    });

    // Handle authentication response
    socketInstance.on('auth_response', (data) => {
      console.log('Authentication response:', data);
      setAuthResponse(data);
    });

    // Handle room subscription response
    socketInstance.on('subscribe_response', (data) => {
      console.log('Room subscription response:', data);
    });

    // Handle room unsubscription response
    socketInstance.on('unsubscribe_response', (data) => {
      console.log('Room unsubscription response:', data);
    });

    // Update computer status when received
    socketInstance.on('computer:status_updated', (data) => {
      console.log('Status update received for computer:', data.computerId);
      setComputerStatuses(prev => ({
        ...prev,
        [data.computerId]: {
          status: data.status,
          cpuUsage: data.cpuUsage,
          ramUsage: data.ramUsage,
          diskUsage: data.diskUsage,
          timestamp: data.timestamp
        }
      }));
    });

    // Connect to the server
    socketInstance.connect();
    setSocket(socketInstance);
    console.log('Socket connection initialized');

    // Cleanup function
    return () => {
      if (socketInstance) {
        socketInstance.disconnect();
      }
    };
  }, [isAuthenticated, user]);

  // Core functionality - subscribe to rooms
  const subscribeToRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    console.log('Subscribing to rooms:', roomIds);
    socket.emit('frontend:subscribe', { roomIds });
  }, [socket, connected]);

  // Core functionality - unsubscribe from rooms
  const unsubscribeFromRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    console.log('Unsubscribing from rooms:', roomIds);
    socket.emit('frontend:unsubscribe', { roomIds });
  }, [socket, connected]);

  // Context value with essential properties and methods
  const contextValue = {
    socket,
    connected,
    computerStatuses,
    authResponse,
    subscribeToRooms,
    unsubscribeFromRooms,
    getComputerStatus: (computerId) => computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0, diskUsage: 0 }
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