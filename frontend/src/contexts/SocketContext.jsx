import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { notification } from 'antd';
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
  const navigate = useNavigate();

  // Initialize socket
  useEffect(() => {
    if (!isAuthenticated) return;

    // Create socket connection
    const socketInstance = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
      autoConnect: false,
      reconnection: true,
      reconnectionAttempts: 5,
      reconnectionDelay: 1000,
    });

    // Set up event listeners
    socketInstance.on('connect', () => {
      console.log('WebSocket connected');
      setConnected(true);
      
      // Authenticate with backend
      socketInstance.emit('frontend:authenticate', {
        token: localStorage.getItem('token')
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
      if (response.status === 'success') {
        console.log('WebSocket authentication successful');
      } else {
        console.error('WebSocket authentication failed:', response.message);
      }
    });

    // Update computer status when received
    socketInstance.on('computer:status_updated', (data) => {
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

    // Set up Admin-specific event listeners
    if (user?.role === 'admin') {
      // Listen for new MFA codes
      socketInstance.on('admin:new_agent_mfa', (data) => {
        notification.info({
          message: 'New Agent MFA',
          description: `Agent ID: ${data.unique_agent_id} requires MFA verification with code: ${data.mfaCode}`,
          duration: 10, // Show for 10 seconds
          onClick: () => {
            navigate('/admin/agents');
          },
        });
      });

      // Listen for new agent registrations
      socketInstance.on('admin:agent_registered', (data) => {
        notification.success({
          message: 'New Agent Registered',
          description: `A new agent (ID: ${data.unique_agent_id}) has been registered. Computer ID: ${data.computerId}`,
          duration: 8,
          onClick: () => {
            navigate('/admin/computers');
          },
        });
      });
    }

    // Connect to the server
    socketInstance.connect();
    setSocket(socketInstance);

    // Cleanup function
    return () => {
      if (socketInstance) {
        socketInstance.disconnect();
      }
    };
  }, [isAuthenticated, user?.role, navigate]);

  // Subscribe to rooms (for computer status updates)
  const subscribeToRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    
    socket.emit('frontend:subscribe', { roomIds });
  }, [socket, connected]);

  // Unsubscribe from rooms
  const unsubscribeFromRooms = useCallback((roomIds) => {
    if (!socket || !connected) return;
    
    socket.emit('frontend:unsubscribe', { roomIds });
  }, [socket, connected]);

  // Send a command to an agent
  const sendCommand = useCallback((computerId, command) => {
    if (!socket || !connected) return;
    
    return new Promise((resolve) => {
      socket.emit('frontend:send_command', { computerId, command });
      
      const onCommandSent = (response) => {
        if (response.computerId === computerId) {
          socket.off('command_sent', onCommandSent);
          resolve(response);
        }
      };
      
      socket.on('command_sent', onCommandSent);
    });
  }, [socket, connected]);

  // Context value
  const contextValue = {
    socket,
    connected,
    computerStatuses,
    subscribeToRooms,
    unsubscribeFromRooms,
    sendCommand
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