/**
 * @fileoverview Command Handle Context Provider for managing remote command execution
 * 
 * This context handles sending commands to remote computers through WebSocket connection,
 * tracking command execution promises, storing command history, and managing command results.
 * It provides a unified interface for executing commands on remote computers and handling
 * their results in a consistent way.
 * 
 * @module CommandHandleContext
 */
import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useMemo,
  useRef,
} from "react";
import { useSocket } from "./SocketContext";

const CommandHandleContext = createContext(null);

/**
 * Command Handle Provider Component
 * 
 * Provides functionality to send commands to remote computers through WebSocket,
 * track command results, manage command history, and handle timeouts.
 * 
 * @component
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components
 * @returns {React.ReactElement} Command handle provider component
 */
export const CommandHandleProvider = ({ children }) => {
  const [commandResults, setCommandResults] = useState({});
  const commandPromisesRef = useRef({});
  const commandsHistoryRef = useRef({}); // Store commands by commandId for reference

  const { socket, isSocketReady } = useSocket();

  /**
   * Sets up event listeners for command completion events from WebSocket
   * 
   * When a command completes on a remote computer:
   * 1. The result is stored in the commandResults state
   * 2. The associated Promise is resolved with the command result
   * 3. The command history is maintained for reference
   * 
   * @effect
   * @listens socket - The Socket.IO instance from SocketContext
   * @listens isSocketReady - Whether the socket is authenticated and ready
   */
  useEffect(() => {
    if (socket && isSocketReady) {
      console.log(
        `[CMD] Socket is ready. Registering command listeners for socket: ${socket.id}`
      );

      const handleCommandCompleted = (data) => {
        console.log("[CMD] Received command:completed:", data);
        if (data?.computerId && data.commandId) {
          setCommandResults((prevResults) => {
            const computerResults = prevResults[data.computerId] || [];

            // Get the original command text from our ref if available
            const commandText =
              commandsHistoryRef.current[data.commandId]?.command || null;
            const commandType =
              commandsHistoryRef.current[data.commandId]?.commandType || data.type || 'console';

            const newResult = {
              type: data.type || commandType || 'console',
              success: data.success === true,
              result: data.result,
              timestamp: Date.now(),
              commandId: data.commandId,
              commandText: commandText,
            };

            return {
              ...prevResults,
              [data.computerId]: [...computerResults, newResult],
            };
          });

          const promiseCallbacks = commandPromisesRef.current[data.commandId];
          if (promiseCallbacks) {
            console.log(
              `[CMD] Found promise callbacks for commandId: ${data.commandId}. Attempting to resolve...`
            );
            promiseCallbacks.resolve(data);
            console.log(
              `[CMD] Promise resolved for commandId: ${data.commandId}`
            );
          } else {
            console.warn(
              `[CMD] No promise callbacks found for completed commandId: ${data.commandId}`
            );
          }
        } else {
          console.warn(
            "[CMD] Invalid 'command:completed' data received:",
            data
          );
        }
      };

      socket.on("command:completed", handleCommandCompleted);

      return () => {
        if (socket) {
          console.log(
            `[CMD] Socket changed or unmounted. Unregistering command listeners for socket: ${socket.id}`
          );
          socket.off("command:completed", handleCommandCompleted);
        }
      };
    } else {
      console.log(
        `[CMD] Socket not ready (isSocketReady: ${isSocketReady}), command listeners not registered.`
      );
    }
  }, [socket, isSocketReady]);

  /**
   * Sends a command to a remote computer and returns a Promise for the result
   * 
   * This function:
   * 1. Validates socket connection and input parameters
   * 2. Constructs and sends the command payload to the server
   * 3. Creates a Promise that resolves when the command completes
   * 4. Sets up timeout handling for commands that don't complete
   * 5. Tracks command history for reference
   * 
   * @function
   * @param {number} computerId - ID of the target computer
   * @param {string} command - Command string to execute on the remote computer
   * @param {string} [commandType='console'] - Type of command (e.g., 'console', 'script')
   * @returns {Promise<Object>} Promise resolving to the command result
   * @returns {string} return.commandId - Unique ID for the command
   * @returns {boolean} return.success - Whether the command executed successfully
   * @returns {string} return.result - Command output text
   * @returns {string} return.type - Command type (e.g., 'console', 'script')
   * @returns {string} return.commandText - Original command text
   * @throws {Error} When socket is not ready or parameters are invalid
   */
  const sendCommand = useCallback(
    (computerId, command, commandType = 'console') => {
      if (!isSocketReady || !socket?.connected) {
        console.error(
          "[CMD] Cannot send command: Socket not ready or not connected."
        );
        return Promise.reject(new Error("Socket not ready or not connected"));
      }
      if (!computerId || !command) {
        console.error(
          "[CMD] Cannot send command: Invalid computerId or command."
        );
        return Promise.reject(
          new Error("Computer ID and command are required")
        );
      }

      console.log(
        `[CMD] Sending command to computer ${computerId}: "${command}" (type: ${commandType})`
      );

      return new Promise((resolve, reject) => {
        const payload = { computerId, command, commandType };
        const TIMEOUT_DURATION = 30000;
        let commandId = null;
        let timeoutId = null;

        const cleanupPromise = (idToClean) => {
          if (idToClean && commandPromisesRef.current[idToClean]) {
            const callbacks = commandPromisesRef.current[idToClean];
            if (callbacks.timeoutId) {
              clearTimeout(callbacks.timeoutId);
            }
            delete commandPromisesRef.current[idToClean];
            console.log(
              `[CMD] Cleaned up promise ref for commandId: ${idToClean}`
            );
          }
        };

        const promiseResolve = (value) => {
          cleanupPromise(commandId);
          resolve({
            ...value,
            commandText: command, // Include the command text in the resolved value
            commandType, // Include the command type in the resolved value
          });
        };

        const promiseReject = (reason) => {
          console.warn(
            `[CMD] Rejecting promise for commandId: ${
              commandId || "(unknown - ack failed?)"
            }. Reason:`,
            reason?.message
          );
          cleanupPromise(commandId);
          reject(reason);
        };

        socket.emit("frontend:send_command", payload, (ack) => {
          if (ack?.status === "success" && ack?.commandId) {
            commandId = ack.commandId;
            console.log(
              `[CMD] Command sent, server acknowledged with commandId: ${commandId}. Storing promise callbacks.`
            );

            // Store the command text associated with this commandId
            commandsHistoryRef.current[commandId] = {
              command: command,
              computerId: computerId,
              commandType: commandType,
              timestamp: Date.now(),
            };

            timeoutId = setTimeout(() => {
              console.warn(
                `[CMD] Command ${commandId} timed out after ${
                  TIMEOUT_DURATION / 1000
                }s.`
              );
              delete commandsHistoryRef.current[commandId]; // Clean up the command history reference
              promiseReject(
                new Error(`Command timed out (${TIMEOUT_DURATION / 1000}s)`)
              );
            }, TIMEOUT_DURATION);

            commandPromisesRef.current[commandId] = {
              resolve: promiseResolve,
              reject: promiseReject,
              timeoutId: timeoutId,
            };
          } else {
            const errorMessage =
              ack?.message ||
              "Server communication error (acknowledgement failed or missing commandId)";
            console.error("[CMD] Server acknowledgement error:", ack);
            reject(new Error(errorMessage));
          }
        });
      });
    },
    [socket, isSocketReady]
  );

  /**
   * Periodically cleans up old command history entries
   * Prevents memory leaks by removing command history entries that are 
   * older than the specified age threshold.
   * 
   * @effect
   */
  useEffect(() => {
    const HISTORY_CLEANUP_INTERVAL = 30 * 60 * 1000; // 30 minutes
    const HISTORY_MAX_AGE = 2 * 60 * 60 * 1000; // 2 hours

    const cleanupInterval = setInterval(() => {
      const now = Date.now();
      const oldCommandIds = Object.keys(commandsHistoryRef.current).filter(
        (cmdId) => {
          const entry = commandsHistoryRef.current[cmdId];
          return entry && now - entry.timestamp > HISTORY_MAX_AGE;
        }
      );

      if (oldCommandIds.length > 0) {
        console.log(
          `[CMD] Cleaning up ${oldCommandIds.length} old command history entries`
        );
        oldCommandIds.forEach((cmdId) => {
          delete commandsHistoryRef.current[cmdId];
        });
      }
    }, HISTORY_CLEANUP_INTERVAL);

    return () => clearInterval(cleanupInterval);
  }, []);

  /**
   * Clears command results for a specific computer
   * 
   * @function
   * @param {number} computerId - ID of the computer to clear results for
   * @param {number|null} [resultIndex=null] - Optional index of specific result to clear
   *                                          If null, clears all results for the computer
   * @returns {void}
   */
  const clearResult = useCallback((computerId, resultIndex = null) => {
    setCommandResults((prevResults) => {
      if (!prevResults[computerId]) return prevResults;

      if (resultIndex !== null && Array.isArray(prevResults[computerId])) {
        const newComputerResults = [...prevResults[computerId]];
        if (resultIndex >= 0 && resultIndex < newComputerResults.length) {
          newComputerResults.splice(resultIndex, 1);
          console.log(
            `[CMD] Cleared result ${resultIndex} for computerId: ${computerId}`
          );

          if (newComputerResults.length === 0) {
            const newResults = { ...prevResults };
            delete newResults[computerId];
            return newResults;
          } else {
            return {
              ...prevResults,
              [computerId]: newComputerResults,
            };
          }
        }
        return prevResults;
      } else {
        const newResults = { ...prevResults };
        delete newResults[computerId];
        console.log(`[CMD] Cleared all results for computerId: ${computerId}`);
        return newResults;
      }
    });
  }, []);

  /**
   * Clears all command results across all computers
   * 
   * @function
   * @returns {void}
   */
  const clearAllResults = useCallback(() => {
    console.log("[CMD] Clearing all command results.");
    setCommandResults({});
  }, []);

  /**
   * Periodically removes expired command results
   * Results older than EXPIRY_TIME (30 minutes) are automatically removed
   * 
   * @effect
   * @listens commandResults - The current state of command results
   */
  useEffect(() => {
    const EXPIRY_TIME = 30 * 60 * 1000;
    const interval = setInterval(() => {
      const now = Date.now();
      let changedResults = false;

      const newResults = Object.entries(commandResults).reduce(
        (acc, [compId, resultArray]) => {
          if (!Array.isArray(resultArray)) {
            if (resultArray && now - resultArray.timestamp <= EXPIRY_TIME) {
              acc[compId] = [resultArray];
            } else {
              changedResults = true;
            }
          } else {
            const filteredResults = resultArray.filter(
              (result) => now - result.timestamp <= EXPIRY_TIME
            );

            if (filteredResults.length > 0) {
              if (filteredResults.length !== resultArray.length) {
                changedResults = true;
              }
              acc[compId] = filteredResults;
            } else {
              changedResults = true;
            }
          }
          return acc;
        },
        {}
      );

      if (changedResults) {
        console.log("[CMD] Pruning expired command results.");
        setCommandResults(newResults);
      }
    }, 5 * 60 * 1000);

    return () => clearInterval(interval);
  }, [commandResults]);

  /**
   * Gets the most recent command result for a specific computer
   * 
   * @function
   * @param {number} computerId - ID of the computer to get results for
   * @returns {Object|null} Most recent command result or null if none exists
   * @returns {string} return.type - Command type (e.g., 'console', 'script')
   * @returns {boolean} return.success - Whether the command executed successfully
   * @returns {string} return.result - Command output text
   * @returns {number} return.timestamp - Timestamp when the result was received
   * @returns {string} return.commandId - Unique ID for the command
   * @returns {string} return.commandText - Original command text
   */
  const getLatestResult = useCallback(
    (computerId) => {
      const results = commandResults[computerId];
      if (!results) return null;

      if (Array.isArray(results) && results.length > 0) {
        return results[results.length - 1];
      }

      if (!Array.isArray(results)) {
        return results;
      }

      return null;
    },
    [commandResults]
  );

  const contextValue = useMemo(
    () => ({
      /**
       * All command results organized by computer ID
       * @type {Object.<number, Array<Object>>}
       */
      commandResults,
      
      /**
       * Get the most recent command result for a computer
       * @type {function(number): Object|null}
       */
      getLatestResult,
      
      /**
       * Clear specific or all results for a computer
       * @type {function(number, number=): void}
       */
      clearResult,
      
      /**
       * Clear all command results for all computers
       * @type {function(): void}
       */
      clearAllResults,
      
      /**
       * Send a command to a remote computer
       * @type {function(number, string, string=): Promise<Object>}
       */
      sendCommand,
    }),
    [commandResults, getLatestResult, clearResult, clearAllResults, sendCommand]
  );

  return (
    <CommandHandleContext.Provider value={contextValue}>
      {children}
    </CommandHandleContext.Provider>
  );
};

/**
 * Hook for accessing the command handle context
 * 
 * @function
 * @returns {Object} Command handle context value
 * @returns {Object.<number, Array<Object>>} return.commandResults - All command results organized by computer ID
 * @returns {function(number): Object|null} return.getLatestResult - Get latest result for a computer
 * @returns {function(number, number=): void} return.clearResult - Clear specific result(s) for a computer
 * @returns {function(): void} return.clearAllResults - Clear all results for all computers
 * @returns {function(number, string, string=): Promise<Object>} return.sendCommand - Send command to computer
 * @throws {Error} If used outside of a CommandHandleProvider
 */
export const useCommandHandle = () => {
  const context = useContext(CommandHandleContext);
  if (!context) {
    throw new Error(
      "useCommandHandle must be used within a CommandHandleProvider"
    );
  }
  return context;
};
