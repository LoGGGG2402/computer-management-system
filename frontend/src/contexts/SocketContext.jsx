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
  const [commandStatus, setCommandStatus] = useState({});
  const { user, isAuthenticated } = useAuth();
  
  // Track command promises to resolve them when responses come back
  const [commandPromises, setCommandPromises] = useState({});

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

    // Handle command sent confirmation
    socketInstance.on('command_sent', (data) => {
      console.log('Command sent status:', data);
      
      // Update command status
      setCommandStatus(prev => ({
        ...prev,
        [data.commandId]: {
          ...data,
          timestamp: Date.now()
        }
      }));
      
      // Resolve the corresponding promise
      if (commandPromises[data.commandId]) {
        if (data.status === 'success') {
          commandPromises[data.commandId].resolve(data);
        } else {
          commandPromises[data.commandId].reject(new Error(data.message || 'Failed to send command'));
        }
        
        // Remove the promise from the tracking object
        setCommandPromises(prev => {
          const newPromises = { ...prev };
          delete newPromises[data.commandId];
          return newPromises;
        });
      }
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

  // Core functionality - send a command to an agent
  const sendCommand = useCallback((computerId, command) => {
    if (!socket || !connected) {
      return Promise.reject(new Error('Not connected to the server'));
    }
    
    console.log(`Sending command to computer ${computerId}: ${command}`);
    
    // Generate a new promise for this command
    return new Promise((resolve, reject) => {
      // Let the backend generate the commandId
      socket.emit('frontend:send_command', { 
        computerId, 
        command 
      });
      
      // Store the promise callbacks for resolution when we get the response
      const tempCommandId = `temp_${Date.now()}`;
      setCommandPromises(prev => ({
        ...prev,
        [tempCommandId]: { resolve, reject }
      }));
      
      // Set a timeout to reject the promise if no response is received
      setTimeout(() => {
        setCommandPromises(prev => {
          if (prev[tempCommandId]) {
            prev[tempCommandId].reject(new Error('Command timed out'));
            const newPromises = { ...prev };
            delete newPromises[tempCommandId];
            return newPromises;
          }
          return prev;
        });
      }, 10000); // 10 second timeout
    });
  }, [socket, connected]);

  // Context value with essential properties and methods
  const contextValue = {
    socket,
    connected,
    computerStatuses,
    authResponse,
    commandStatus,
    subscribeToRooms,
    unsubscribeFromRooms,
    sendCommand,
    getComputerStatus: (computerId) => computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0 }
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