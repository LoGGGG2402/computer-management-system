/**
 * @fileoverview Socket Context Provider for managing WebSocket connections.
 *
 * This context provides real-time communication with the server through Socket.IO,
 * handling authentication, computer subscription management, and status updates.
 *
 * @module SocketContext
 */
import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  useMemo,
  useRef,
} from "react";
import io from "socket.io-client";
import { useAuth } from "./AuthContext";

const SocketContext = createContext(null);

/**
 * Socket Provider Component.
 *
 * Establishes and manages WebSocket connections with the backend server.
 * Handles authentication, reconnection, and maintains computer status information.
 *
 * @component
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components
 * @returns {React.ReactElement} Socket provider component
 */
export const SocketProvider = ({ children }) => {
  const socketRef = useRef(null);
  const [computerStatuses, setComputerStatuses] = useState({});
  const [isSocketReady, setIsSocketReady] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);

  const { isAuthenticated, user } = useAuth();

  /**
   * Establishes and manages the WebSocket connection based on authentication state.
   */
  useEffect(() => {
    if (isAuthenticated && user?.token) {
      if (!socketRef.current && !isConnecting) {
        setIsConnecting(true);
        setIsSocketReady(false);
        setComputerStatuses({});

        console.log("[SocketContext] Attempting to connect WebSocket...");

        const newSocket = io(
          import.meta.env.VITE_API_URL || "http://localhost:3000",
          {
            reconnection: true,
            reconnectionAttempts: 5,
            reconnectionDelay: 1000,
            extraHeaders: {
              "X-Client-Type": "frontend",
              Authorization: `Bearer ${user.token}`,
            },
          }
        );
        socketRef.current = newSocket;

        newSocket.on("connect", () => {
          console.log(
            "[SocketContext] WebSocket connected. Socket ID:",
            newSocket.id
          );
          setIsSocketReady(true);
          setIsConnecting(false);
          setComputerStatuses({});

        });

        newSocket.on("disconnect", (reason) => {
          console.log(
            "[SocketContext] WebSocket disconnected. Reason:",
            reason
          );
          setIsSocketReady(false);
          setIsConnecting(false);
          setComputerStatuses({});
        });

        newSocket.on("connect_error", (err) => {
          console.error(
            "[SocketContext] WebSocket connection error:",
            err.message
          );
          setIsSocketReady(false);
          setIsConnecting(false);
          socketRef.current = null;
        });

        newSocket.on("auth_error", (err) => {
          console.error("[SocketContext] Received auth_error event:", err);
          setIsSocketReady(false);
          setIsConnecting(false);
          newSocket.disconnect();
        });

        newSocket.on("computer:status_updated", (data) => {
          console.log(
            "[SocketContext] Received computer:status_updated event:",
            data
          );
          if (data && data.computerId) {
            setComputerStatuses((prev) => ({
              ...prev,
              [data.computerId]: {
                status: data.status,
                cpuUsage: data.cpuUsage,
                ramUsage: data.ramUsage,
                diskUsage: data.diskUsage,
                timestamp: data.timestamp || Date.now(),
              },
            }));
          }
        });

        newSocket.on("subscribe_response", (data) => {
          console.log("[SocketContext] Received subscribe_response:", data);
          if (data.status === "error") {
            console.error(
              `[SocketContext] Failed to subscribe to computer ${data.computerId}: ${data.message}`
            );
          }
        });

        newSocket.on("unsubscribe_response", (data) => {
          console.log("[SocketContext] Received unsubscribe_response:", data);
          if (data.status === "success" && data.computerId) {
            setComputerStatuses((prev) => {
              const newState = { ...prev };
              delete newState[data.computerId];
              return newState;
            });
          } else if (data.status === "error") {
            console.error(
              `[SocketContext] Failed to unsubscribe from computer ${data.computerId}: ${data.message}`
            );
          }
        });

        return () => {
          clearTimeout(authTimeout);
        };
      }
    } else {
      if (socketRef.current) {
        console.log(
          "[SocketContext] User not authenticated or token missing. Disconnecting WebSocket."
        );
        socketRef.current.disconnect();
        socketRef.current = null;
        setIsSocketReady(false);
        setIsConnecting(false);
        setComputerStatuses({});
      }
    }

    return () => {
      if (socketRef.current) {
        console.log("[SocketContext] Cleaning up WebSocket connection.");
        socketRef.current.off("connect");
        socketRef.current.off("disconnect");
        socketRef.current.off("connect_error");
        socketRef.current.off("auth_error");
        socketRef.current.off("computer:status_updated");
        socketRef.current.off("subscribe_response");
        socketRef.current.off("unsubscribe_response");
        socketRef.current.disconnect();
        socketRef.current = null;
      }
      setIsSocketReady(false);
      setIsConnecting(false);
      setComputerStatuses({});
    };
  }, [isAuthenticated, user?.token]);

  /**
   * Subscribes to status updates for a specific computer.
   * Event: `frontend:subscribe`, Data: `{ computerId: "integer" }`
   * @param {number|string} computerId - ID of the computer to subscribe to.
   */
  const subscribeToComputer = useCallback(
    (computerId) => {
      if (isSocketReady && socketRef.current?.connected && computerId) {
        console.log(
          `[SocketContext] Emitting frontend:subscribe for computerId: ${computerId}`
        );
        socketRef.current.emit("frontend:subscribe", {
          computerId: Number(computerId),
        });
      } else {
        console.warn(
          `[SocketContext] Cannot subscribe: Socket not ready (isSocketReady: ${isSocketReady}, connected: ${socketRef.current?.connected}) or computerId missing.`
        );
      }
    },
    [isSocketReady]
  );

  /**
   * Unsubscribes from status updates for a specific computer.
   * Event: `frontend:unsubscribe`, Data: `{ computerId: "integer" }`
   * @param {number|string} computerId - ID of the computer to unsubscribe from.
   */
  const unsubscribeFromComputer = useCallback(
    (computerId) => {
      if (isSocketReady && socketRef.current?.connected && computerId) {
        console.log(
          `[SocketContext] Emitting frontend:unsubscribe for computerId: ${computerId}`
        );
        socketRef.current.emit("frontend:unsubscribe", {
          computerId: Number(computerId),
        });
      } else {
        console.warn(
          `[SocketContext] Cannot unsubscribe: Socket not ready or computerId missing.`
        );
      }
    },
    [isSocketReady]
  );

  /**
   * Gets the current status for a specific computer.
   * @param {number|string} computerId - ID of the computer.
   * @returns {Object} Computer status object.
   */
  const getComputerStatus = useCallback(
    (computerId) => {
      return (
        computerStatuses[computerId] || {
          status: "offline",
          cpuUsage: 0,
          ramUsage: 0,
          diskUsage: 0,
          timestamp: 0,
        }
      );
    },
    [computerStatuses]
  );

  const contextValue = useMemo(
    () => ({
      socket: socketRef.current,
      isSocketReady,
      isConnecting,
      subscribeToComputer,
      unsubscribeFromComputer,
      getComputerStatus,
      computerStatuses,
    }),
    [
      isSocketReady,
      isConnecting,
      subscribeToComputer,
      unsubscribeFromComputer,
      getComputerStatus,
      computerStatuses,
    ]
  );

  return (
    <SocketContext.Provider value={contextValue}>
      {children}
    </SocketContext.Provider>
  );
};

/**
 * Hook for accessing the socket context.
 *
 * @returns {Object} Socket context value.
 * @throws {Error} If used outside of a SocketProvider.
 */
export const useSocket = () => {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error("useSocket must be used within a SocketProvider");
  }
  return context;
};
