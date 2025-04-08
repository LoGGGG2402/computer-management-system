import React, { createContext, useContext, useState, useEffect } from 'react';
import { useSocket } from './SocketContext';

// Create context
const CommandResultContext = createContext(null);

/**
 * Provider component for managing command results across the application
 * Tracks the latest command result for each computer
 */
export const CommandResultProvider = ({ children }) => {
  // Store results by computerId: { computerId: { stdout, stderr, exitCode, timestamp } }
  const [commandResults, setCommandResults] = useState({});
  const { socket, connected } = useSocket();

  // Listen for command completion events from socket
  useEffect(() => {
    if (!socket || !connected) return;

    const handleCommandCompleted = (data) => {
      console.log('[CommandResultContext] Received command result:', data);
      
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

    // Register event listener
    socket.on('command:completed', handleCommandCompleted);

    // Cleanup
    return () => {
      socket.off('command:completed', handleCommandCompleted);
    };
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
    clearResult,
    clearAllResults
  };

  return (
    <CommandResultContext.Provider value={contextValue}>
      {children}
    </CommandResultContext.Provider>
  );
};

// Custom hook to use the context
export const useCommandResults = () => {
  const context = useContext(CommandResultContext);
  if (!context) {
    throw new Error('useCommandResults must be used within a CommandResultProvider');
  }
  return context;
};

export default CommandResultContext;