import React, { createContext, useContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useSocket } from './SocketContext';

const CommandHandleContext = createContext(null);

export const CommandHandleProvider = ({ children }) => {
  const [commandResults, setCommandResults] = useState({});
  const commandPromisesRef = useRef({});
  const [commandStatus, setCommandStatus] = useState({});

  const { socket } = useSocket();
  const isConnected = socket?.connected;

  useEffect(() => {
    if (socket && isConnected) {
      console.log(`[CMD] Registering listeners for socket: ${socket.id}`);

      const handleCommandCompleted = (data) => {
        console.log('[CMD] Received command result:', data);
        if (data?.computerId && data.commandId) {
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
             const promiseCallbacks = commandPromisesRef.current[data.commandId];
             if (promiseCallbacks) {
                 promiseCallbacks.resolve(data);
                 if (promiseCallbacks.timeoutId) clearTimeout(promiseCallbacks.timeoutId);
                 delete commandPromisesRef.current[data.commandId];
             }
        } else {
             console.warn("[CMD] Invalid 'command:completed' data:", data);
        }
      };

      const handleCommandSentStatus = (data) => {
        console.log('[CMD] Received command sent status:', data);
         if (data?.commandId) {
             setCommandStatus(prev => ({
               ...prev,
               [data.commandId]: {
                 status: data.status,
                 message: data.message,
                 timestamp: Date.now()
               }
             }));

             const promiseCallbacks = commandPromisesRef.current[data.commandId];
             if (promiseCallbacks && data.status === 'error') {
                 promiseCallbacks.reject(new Error(data.message || 'Failed to send command to agent'));
                 if (promiseCallbacks.timeoutId) clearTimeout(promiseCallbacks.timeoutId);
                 delete commandPromisesRef.current[data.commandId];
             } else if (promiseCallbacks && data.status === 'success') {
                 console.log(`[CMD] Command ${data.commandId} sent successfully to agent.`);
             }
         } else {
              console.warn("[CMD] Invalid 'command_sent' data:", data);
         }
      };

      socket.on('command:completed', handleCommandCompleted);
      socket.on('command_sent', handleCommandSentStatus);

      return () => {
        console.log(`[CMD] Unregistering listeners for socket: ${socket.id}`);
        socket.off('command:completed', handleCommandCompleted);
        socket.off('command_sent', handleCommandSentStatus);
      };
    } else {
       console.log('[CMD] Socket not ready, listeners not registered.');
    }
  }, [socket, isConnected]);

  const sendCommand = useCallback((computerId, command) => {
    const currentSocket = socket;
    const currentIsConnected = currentSocket?.connected;

    if (!currentIsConnected || !currentSocket) {
      console.error('[CMD] Cannot send command: Socket not connected.');
      return Promise.reject(new Error('Socket not connected'));
    }
    if (!computerId || !command) {
        console.error('[CMD] Cannot send command: Invalid computerId or command.');
        return Promise.reject(new Error('Computer ID and command are required'));
    }

    console.log(`[CMD] Sending command to computer ${computerId}: ${command}`);

    return new Promise((resolve, reject) => {
      const payload = { computerId, command };
      const TIMEOUT_DURATION = 30000;
      let commandId = null;
      let timeoutId = null;

      const cleanupPromise = (id) => {
          if (id && commandPromisesRef.current[id]) {
              if (commandPromisesRef.current[id].timeoutId) {
                  clearTimeout(commandPromisesRef.current[id].timeoutId);
              }
              delete commandPromisesRef.current[id];
          }
      };

      const promiseResolve = (value) => {
          cleanupPromise(commandId);
          resolve(value);
      };

      const promiseReject = (reason) => {
          cleanupPromise(commandId);
          reject(reason);
      };


      currentSocket.emit('frontend:send_command', payload, (ack) => {
          if (ack?.commandId) {
              commandId = ack.commandId;
              console.log(`[CMD] Command received by server, commandId: ${commandId}`);

              timeoutId = setTimeout(() => {
                if (commandPromisesRef.current[commandId]) {
                  console.warn(`[CMD] Command ${commandId} timed out waiting for completion.`);
                  promiseReject(new Error(`Command timed out (${TIMEOUT_DURATION/1000}s)`));
                }
              }, TIMEOUT_DURATION);

              commandPromisesRef.current[commandId] = { resolve: promiseResolve, reject: promiseReject, timeoutId: timeoutId };

          } else {
              console.error('[CMD] Server did not return commandId in acknowledgement.');
              reject(new Error('Server communication error (no commandId).'));
          }
      });
    });
  }, [socket]);

  const clearResult = useCallback((computerId) => {
    setCommandResults(prevResults => {
      if (prevResults[computerId]) {
          const newResults = { ...prevResults };
          delete newResults[computerId];
          return newResults;
      }
      return prevResults;
    });
  }, []);

  const clearAllResults = useCallback(() => {
    setCommandResults({});
    setCommandStatus({});
  }, []);

  useEffect(() => {
    const EXPIRY_TIME = 30 * 60 * 1000;
    const interval = setInterval(() => {
      const now = Date.now();
      let changedResults = false;
      let changedStatus = false;

      const newResults = Object.entries(commandResults).reduce((acc, [computerId, result]) => {
        if (now - result.timestamp <= EXPIRY_TIME) {
          acc[computerId] = result;
        } else {
          changedResults = true;
        }
        return acc;
      }, {});

      const newStatus = Object.entries(commandStatus).reduce((acc, [commandId, status]) => {
           if(now - status.timestamp <= EXPIRY_TIME) {
               acc[commandId] = status;
           } else {
               changedStatus = true;
           }
           return acc;
       }, {});

      if (changedResults) {
        setCommandResults(newResults);
      }
      if (changedStatus) {
        setCommandStatus(newStatus);
      }
    }, 5 * 60 * 1000); // Check every 5 minutes
    return () => clearInterval(interval);
  }, [commandResults, commandStatus]);

  const contextValue = useMemo(() => ({
    commandResults,
    commandStatus,
    clearResult,
    clearAllResults,
    sendCommand
  }), [commandResults, commandStatus, clearResult, clearAllResults, sendCommand]);

  return (
    <CommandHandleContext.Provider value={contextValue}>
      {children}
    </CommandHandleContext.Provider>
  );
};

export const useCommandHandle = () => {
  const context = useContext(CommandHandleContext);
  if (!context) {
    throw new Error('useCommandHandle must be used within a CommandHandleProvider');
  }
  return context;
};
