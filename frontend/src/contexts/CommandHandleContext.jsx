/**
 * @fileoverview Command Handle Context Provider for managing remote command execution.
 *
 * This context handles sending commands to remote computers through WebSocket connection,
 * tracking command execution promises, storing command history, and managing command results.
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

const COMMAND_TIMEOUT_DURATION = 30000; // 30 seconds
const HISTORY_CLEANUP_INTERVAL = 30 * 60 * 1000; // 30 minutes
const HISTORY_MAX_AGE = 2 * 60 * 60 * 1000; // 2 hours
const RESULTS_EXPIRY_TIME = 30 * 60 * 1000; // 30 minutes
const RESULTS_PRUNING_INTERVAL = 5 * 60 * 1000; // 5 minutes

/**
 * Command Handle Provider Component.
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
  const commandsHistoryRef = useRef({});

  const { socket, isSocketReady } = useSocket();

  /**
   * Sets up event listeners for command completion events from WebSocket.
   * Event: `command:completed`
   * Data: { commandId, computerId, type, success, result, timestamp } (from API doc)
   */
  useEffect(() => {
    if (socket && isSocketReady) {
      console.log(
        `[CommandHandleContext] Socket is ready. Registering 'command:completed' listener for socket: ${socket.id}`
      );

      const handleCommandCompleted = (data) => {
        console.log(
          "[CommandHandleContext] Received 'command:completed':",
          data
        );
        if (data?.commandId && data.hasOwnProperty("computerId")) {
          const { commandId, computerId, type, success, result } = data;

          const originalCommandInfo = commandsHistoryRef.current[commandId];
          const commandText = originalCommandInfo?.command || null;
          const originalCommandType =
            originalCommandInfo?.commandType || "console";

          const newResultEntry = {
            commandId,
            computerId,
            type: type || originalCommandType,
            success: success === true,
            result: result,
            timestamp: data.timestamp || Date.now(),
            commandText: commandText,
          };

          setCommandResults((prevResults) => {
            const computerResults = prevResults[computerId] || [];
            return {
              ...prevResults,
              [computerId]: [...computerResults, newResultEntry],
            };
          });

          const promiseCallbacks = commandPromisesRef.current[commandId];
          if (promiseCallbacks) {
            console.log(
              `[CommandHandleContext] Found promise callbacks for commandId: ${commandId}. Resolving.`
            );
            if (promiseCallbacks.timeoutId) {
              clearTimeout(promiseCallbacks.timeoutId);
            }
            promiseCallbacks.resolve(newResultEntry);
            delete commandPromisesRef.current[commandId];
          } else {
            console.warn(
              `[CommandHandleContext] No promise callbacks found for completed commandId: ${commandId}. Result stored.`
            );
          }
        } else {
          console.warn(
            "[CommandHandleContext] Invalid 'command:completed' data received (missing commandId or computerId):",
            data
          );
        }
      };

      socket.on("command:completed", handleCommandCompleted);

      return () => {
        if (socket) {
          console.log(
            `[CommandHandleContext] Cleaning up 'command:completed' listener for socket: ${socket.id}`
          );
          socket.off("command:completed", handleCommandCompleted);
        }
      };
    } else {
      console.log(
        `[CommandHandleContext] Socket not ready (isSocketReady: ${isSocketReady}), 'command:completed' listener not registered.`
      );
    }
  }, [socket, isSocketReady]);

  /**
   * Sends a command to a remote computer.
   * Event: `frontend:send_command`
   * Data: { computerId, command, commandType }
   * Ack Response: { status, message, computerId, commandId, commandType }
   *
   * @param {number|string} computerId - ID of the target computer.
   * @param {string} command - Command string to execute.
   * @param {string} [commandType='console'] - Type of command (from API doc: 'console').
   * @returns {Promise<Object>} Promise resolving to the command result object.
   * @throws {Error} When socket is not ready or parameters are invalid.
   */
  const sendCommand = useCallback(
    (computerId, command, commandType = "console") => {
      if (!isSocketReady || !socket?.connected) {
        const errorMsg =
          "[CommandHandleContext] Cannot send command: Socket not ready or not connected.";
        console.error(errorMsg);
        return Promise.reject(new Error("Socket not ready or not connected"));
      }
      if (computerId === null || computerId === undefined || !command) {
        const errorMsg =
          "[CommandHandleContext] Cannot send command: Invalid computerId or command.";
        console.error(errorMsg);
        return Promise.reject(
          new Error("Computer ID and command are required")
        );
      }
      if (commandType !== "console") {
        console.warn(
          `[CommandHandleContext] Sending command with type "${commandType}". API doc specifies 'console'. Ensure backend supports this.`
        );
      }

      console.log(
        `[CommandHandleContext] Sending command to computer ${computerId}: "${command}" (type: ${commandType})`
      );

      return new Promise((resolve, reject) => {
        const payload = {
          computerId: Number(computerId),
          command,
          commandType,
        };
        let localCommandId = null;
        let cmdTimeoutId = null;

        const cleanupPromise = (idToClean) => {
          if (idToClean && commandPromisesRef.current[idToClean]) {
            const callbacks = commandPromisesRef.current[idToClean];
            if (callbacks.timeoutId) {
              clearTimeout(callbacks.timeoutId);
            }
            delete commandPromisesRef.current[idToClean];
            console.log(
              `[CommandHandleContext] Cleaned up promise ref for commandId: ${idToClean}`
            );
          }
        };

        const wrappedResolve = (value) => {
          cleanupPromise(localCommandId);
          resolve(value);
        };
        const wrappedReject = (reason) => {
          cleanupPromise(localCommandId);
          reject(reason);
        };

        socket.emit("frontend:send_command", payload, (ack) => {
          console.log(
            "[CommandHandleContext] Received ack for 'frontend:send_command':",
            ack
          );
          if (ack?.status === "success" && ack?.commandId) {
            localCommandId = ack.commandId;
            console.log(
              `[CommandHandleContext] Command sent, server acknowledged with commandId: ${localCommandId}. Storing promise callbacks.`
            );

            commandsHistoryRef.current[localCommandId] = {
              command,
              computerId: Number(computerId),
              commandType,
              timestamp: Date.now(),
            };

            cmdTimeoutId = setTimeout(() => {
              console.warn(
                `[CommandHandleContext] Command ${localCommandId} timed out after ${
                  COMMAND_TIMEOUT_DURATION / 1000
                }s.`
              );
              wrappedReject(
                new Error(
                  `Command ${localCommandId} timed out (${
                    COMMAND_TIMEOUT_DURATION / 1000
                  }s)`
                )
              );
            }, COMMAND_TIMEOUT_DURATION);

            commandPromisesRef.current[localCommandId] = {
              resolve: wrappedResolve,
              reject: wrappedReject,
              timeoutId: cmdTimeoutId,
            };
          } else {
            const errorMessage =
              ack?.message ||
              "Server communication error (acknowledgement failed or missing commandId)";
            console.error(
              "[CommandHandleContext] Server acknowledgement error for send_command:",
              ack
            );
            reject(new Error(errorMessage));
          }
        });
      });
    },
    [socket, isSocketReady]
  );

  /**
   * Periodically cleans up old command history entries.
   */
  useEffect(() => {
    const cleanupJob = setInterval(() => {
      const now = Date.now();
      const oldCommandIds = Object.keys(commandsHistoryRef.current).filter(
        (cmdId) => {
          const entry = commandsHistoryRef.current[cmdId];
          return (
            entry &&
            now - entry.timestamp > HISTORY_MAX_AGE &&
            !commandPromisesRef.current[cmdId]
          );
        }
      );

      if (oldCommandIds.length > 0) {
        console.log(
          `[CommandHandleContext] Cleaning up ${oldCommandIds.length} old command history entries.`
        );
        oldCommandIds.forEach((cmdId) => {
          delete commandsHistoryRef.current[cmdId];
        });
      }
    }, HISTORY_CLEANUP_INTERVAL);

    return () => clearInterval(cleanupJob);
  }, []);

  /**
   * Clears command results for a specific computer or a specific result.
   * @param {number|string} computerId - ID of the computer.
   * @param {number|null} [resultIndex=null] - Index of the result to clear. If null, clears all for computer.
   */
  const clearResult = useCallback((computerId, resultIndex = null) => {
    setCommandResults((prevResults) => {
      const compIdStr = String(computerId);
      if (!prevResults[compIdStr]) return prevResults;

      if (resultIndex !== null && Array.isArray(prevResults[compIdStr])) {
        const newComputerResults = [...prevResults[compIdStr]];
        if (resultIndex >= 0 && resultIndex < newComputerResults.length) {
          newComputerResults.splice(resultIndex, 1);
          console.log(
            `[CommandHandleContext] Cleared result ${resultIndex} for computerId: ${compIdStr}`
          );

          if (newComputerResults.length === 0) {
            const { [compIdStr]: _, ...restResults } = prevResults;
            return restResults;
          } else {
            return {
              ...prevResults,
              [compIdStr]: newComputerResults,
            };
          }
        }
      } else {
        const { [compIdStr]: _, ...restResults } = prevResults;
        console.log(
          `[CommandHandleContext] Cleared all results for computerId: ${compIdStr}`
        );
        return restResults;
      }
      return prevResults;
    });
  }, []);

  /**
   * Clears all command results across all computers.
   */
  const clearAllResults = useCallback(() => {
    console.log("[CommandHandleContext] Clearing all command results.");
    setCommandResults({});
  }, []);

  /**
   * Periodically removes expired command results from state.
   */
  useEffect(() => {
    const pruneJob = setInterval(() => {
      const now = Date.now();
      let resultsChanged = false;
      const nextResults = {};

      for (const compId in commandResults) {
        if (commandResults.hasOwnProperty(compId)) {
          const resultArray = commandResults[compId];
          if (Array.isArray(resultArray)) {
            const filtered = resultArray.filter(
              (result) => now - result.timestamp <= RESULTS_EXPIRY_TIME
            );
            if (filtered.length < resultArray.length) {
              resultsChanged = true;
            }
            if (filtered.length > 0) {
              nextResults[compId] = filtered;
            } else if (filtered.length === 0 && resultArray.length > 0) {
              resultsChanged = true;
            }
          }
        }
      }

      if (resultsChanged) {
        console.log("[CommandHandleContext] Pruning expired command results.");
        setCommandResults(nextResults);
      }
    }, RESULTS_PRUNING_INTERVAL);

    return () => clearInterval(pruneJob);
  }, [commandResults]);

  /**
   * Gets the most recent command result for a specific computer.
   * @param {number|string} computerId - ID of the computer.
   * @returns {Object|null} Most recent command result or null.
   */
  const getLatestResult = useCallback(
    (computerId) => {
      const results = commandResults[String(computerId)];
      return Array.isArray(results) && results.length > 0
        ? results[results.length - 1]
        : null;
    },
    [commandResults]
  );

  const contextValue = useMemo(
    () => ({
      commandResults,
      getLatestResult,
      clearResult,
      clearAllResults,
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
 * Hook for accessing the command handle context.
 *
 * @returns {Object} Command handle context value.
 * @throws {Error} If used outside of a CommandHandleProvider.
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
