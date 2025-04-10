import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useSocket } from './SocketContext';

// Create context
const CommandHandleContext = createContext(null);

/**
 * Provider component for managing command execution and results across the application
 * Handles sending commands and tracking the latest command result for each computer
 */
export const CommandHandleProvider = ({ children }) => {
  // Store results by computerId: { computerId: { stdout, stderr, exitCode, timestamp } }
  const [commandResults, setCommandResults] = useState({});
  // Track command promises to resolve them when responses come back
  const [commandPromises, setCommandPromises] = useState({});
  // Track command status
  const [commandStatus, setCommandStatus] = useState({});
  
  const { socket, connected } = useSocket();

  // Listen for command completion events from socket
  useEffect(() => {
    if (!socket || !connected) return;

    const handleCommandCompleted = (data) => {
      console.log('[CommandHandleContext] Received command result:', data);
      
      // Store the result by computerId
      setCommandResults(prevResults => ({
        ...prevResults,
        [data.computerId]: {
          stdout: data.stdout,
          stderr: data.stderr,
          exitCode: data.exitCode,
          timestamp: Date.now(),
          commandId: data.commandId
        }
      }));
    };

    // Register event listener for command completion
    socket.on('command:completed', handleCommandCompleted);
    
    // Handle command sent confirmation
    socket.on('command_sent', (data) => {
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
    
    // Handle room command response
    socket.on('room_command_sent', (data) => {
      console.log('Room command sent status:', data);
      
      // Update command status for room commands
      setCommandStatus(prev => ({
        ...prev,
        [`room_${data.roomId}`]: {
          ...data,
          timestamp: Date.now()
        }
      }));
      
      // Resolve the corresponding promise for room commands
      if (commandPromises[`room_${data.roomId}`]) {
        if (data.status === 'success') {
          commandPromises[`room_${data.roomId}`].resolve(data);
        } else {
          commandPromises[`room_${data.roomId}`].reject(new Error(data.message || 'Failed to send command to room'));
        }
        
        // Remove the promise from the tracking object
        setCommandPromises(prev => {
          const newPromises = { ...prev };
          delete newPromises[`room_${data.roomId}`];
          return newPromises;
        });
      }
    });

    // Cleanup
    return () => {
      socket.off('command:completed', handleCommandCompleted);
      socket.off('command_sent');
      socket.off('room_command_sent');
    };
  }, [socket, connected, commandPromises]);

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
  
  // Send command to all computers in a room
  const sendCommandToRoom = useCallback((roomId, command) => {
    if (!socket || !connected) {
      return Promise.reject(new Error('Not connected to the server'));
    }
    
    console.log(`Sending command to room ${roomId}: ${command}`);
    
    // Generate a new promise for this command
    return new Promise((resolve, reject) => {
      // Emit the room command event to server
      socket.emit('frontend:send_room_command', { 
        roomId, 
        command 
      });
      
      // Store the promise callbacks for resolution when we get the response
      const promiseId = `room_${roomId}`;
      setCommandPromises(prev => ({
        ...prev,
        [promiseId]: { resolve, reject }
      }));
      
      // Set a timeout to reject the promise if no response is received
      setTimeout(() => {
        setCommandPromises(prev => {
          if (prev[promiseId]) {
            prev[promiseId].reject(new Error('Room command timed out'));
            const newPromises = { ...prev };
            delete newPromises[promiseId];
            return newPromises;
          }
          return prev;
        });
      }, 15000); // 15 second timeout for room commands
    });
  }, [socket, connected]);

  // Clear result for a specific computer
  const clearResult = (computerId) => {
    setCommandResults(prevResults => {
      const newResults = { ...prevResults };
      delete newResults[computerId];
      return newResults;
    });
  };

  // Clear all results
  const clearAllResults = () => {
    setCommandResults({});
  };

  // Auto-expire results after 10 minutes
  useEffect(() => {
    const EXPIRY_TIME = 10 * 60 * 1000; // 10 minutes in milliseconds
    
    const interval = setInterval(() => {
      const now = Date.now();
      
      setCommandResults(prevResults => {
        const newResults = { ...prevResults };
        let changed = false;
        
        // Check each result for expiry
        Object.entries(newResults).forEach(([computerId, result]) => {
          if (now - result.timestamp > EXPIRY_TIME) {
            delete newResults[computerId];
            changed = true;
          }
        });
        
        // Only update state if something changed
        return changed ? newResults : prevResults;
      });
    }, 60000); // Check every minute
    
    return () => clearInterval(interval);
  }, []);

  // Context value
  const contextValue = {
    commandResults,
    commandStatus,
    clearResult,
    clearAllResults,
    sendCommand,
    sendCommandToRoom
  };

  return (
    <CommandHandleContext.Provider value={contextValue}>
      {children}
    </CommandHandleContext.Provider>
  );
};

// Custom hook to use the context
export const useCommandHandle = () => {
  const context = useContext(CommandHandleContext);
  if (!context) {
    throw new Error('useCommandHandle must be used within a CommandHandleProvider');
  }
  return context;
};

export default CommandHandleContext;