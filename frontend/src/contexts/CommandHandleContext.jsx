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

export const CommandHandleProvider = ({ children }) => {
  const [commandResults, setCommandResults] = useState({});
  const commandPromisesRef = useRef({});
  const commandsHistoryRef = useRef({}); // Store commands by commandId for reference

  const { socket, isSocketReady } = useSocket();

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

            const newResult = {
              stdout: data.stdout,
              stderr: data.stderr,
              exitCode: data.exitCode,
              timestamp: Date.now(),
              commandId: data.commandId,
              commandText: commandText, // Store the command text with the result
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

  const sendCommand = useCallback(
    (computerId, command) => {
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
        `[CMD] Sending command to computer ${computerId}: "${command}"`
      );

      return new Promise((resolve, reject) => {
        const payload = { computerId, command };
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

  // Clean up old command history entries periodically
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

  const clearAllResults = useCallback(() => {
    console.log("[CMD] Clearing all command results.");
    setCommandResults({});
  }, []);

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

export const useCommandHandle = () => {
  const context = useContext(CommandHandleContext);
  if (!context) {
    throw new Error(
      "useCommandHandle must be used within a CommandHandleProvider"
    );
  }
  return context;
};
