import React, { createContext, useContext, useEffect, useState, useCallback, useMemo, useRef } from 'react';
import io from 'socket.io-client';
import { useAuth } from './AuthContext';

const SocketContext = createContext(null);

export const SocketProvider = ({ children }) => {
  const socketRef = useRef(null);
  const [computerStatuses, setComputerStatuses] = useState({});
  const [isSocketReady, setIsSocketReady] = useState(false);
  const { isAuthenticated, user, logoutAction } = useAuth();

  useEffect(() => {
    if (isAuthenticated && user?.token) {
      if (!socketRef.current || !socketRef.current.connected) {
        if (socketRef.current) {
            console.log('[SocketContext] Disconnecting existing socket before reconnecting.');
            socketRef.current.disconnect();
        }

        console.log('[SocketContext] Attempting to connect socket...');
        setIsSocketReady(false);
        setComputerStatuses({});

        const newSocket = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
          reconnection: true,
          reconnectionAttempts: 5,
          reconnectionDelay: 1000,
          extraHeaders: {
            'X-Client-Type': 'frontend'
          }
        });
        socketRef.current = newSocket;

        newSocket.on('connect', () => {
          console.log(`[SocketContext] Socket connected: ${newSocket.id}. Authenticating...`);
          newSocket.emit('frontend:authenticate', { token: user.token });
        });

        newSocket.on('auth_response', (data) => {
          if (data?.status === 'success') {
            console.log('[SocketContext] Backend authentication successful:', data.message);
            setIsSocketReady(true);
          } else {
            console.error('[SocketContext] Backend authentication failed:', data?.message || 'Unknown error');
            setIsSocketReady(false);
            newSocket.disconnect();
          }
        });

        newSocket.on('disconnect', (reason) => {
          console.log(`[SocketContext] Socket disconnected: ${reason}`);
          setIsSocketReady(false);
          setComputerStatuses({});
        });

        newSocket.on('connect_error', (err) => {
          console.error(`[SocketContext] Socket connection error: ${err.message}`);
          setIsSocketReady(false);
        });

        newSocket.on('computer:status_updated', (data) => {
          if (data && data.computerId) {
             setComputerStatuses(prev => ({
               ...prev,
               [data.computerId]: {
                 status: data.status,
                 cpuUsage: data.cpuUsage,
                 ramUsage: data.ramUsage,
                 diskUsage: data.diskUsage,
                 timestamp: data.timestamp || Date.now()
               }
             }));
          }
        });

        newSocket.on('subscribe_response', (data) => {
          console.log('[SocketContext] Subscribe response:', data);
        });

        newSocket.on('unsubscribe_response', (data) => {
           console.log('[SocketContext] Unsubscribe response:', data);
           if (data.status === 'success' && data.computerId) {
                setComputerStatuses(prev => {
                    const newState = {...prev};
                    delete newState[data.computerId];
                    return newState;
                });
           }
        });

      }
    } else {
      if (socketRef.current?.connected) {
        console.log('[SocketContext] Disconnecting socket due to auth state change (logged out).');
        socketRef.current.disconnect();
        socketRef.current = null;
        setIsSocketReady(false);
        setComputerStatuses({});
      }
    }

    return () => {
      if (socketRef.current) {
        console.log('[SocketContext] Cleaning up socket connection...');
        socketRef.current.off('connect');
        socketRef.current.off('auth_response');
        socketRef.current.off('disconnect');
        socketRef.current.off('connect_error');
        socketRef.current.off('computer:status_updated');
        socketRef.current.off('subscribe_response');
        socketRef.current.off('unsubscribe_response');
        socketRef.current.disconnect();
        socketRef.current = null;
        setIsSocketReady(false);
        setComputerStatuses({});
      }
    };
  }, [isAuthenticated, user?.token, logoutAction]);

  const subscribeToComputer = useCallback((computerId) => {
    if (isSocketReady && socketRef.current?.connected && computerId) {
       console.log(`[SocketContext] Emitting frontend:subscribe for computer ${computerId}`);
       socketRef.current.emit('frontend:subscribe', { computerId });
    } else {
       console.warn(`[SocketContext] Cannot subscribe computer ${computerId}: Socket not ready (isSocketReady: ${isSocketReady}, connected: ${socketRef.current?.connected})`);
    }
  }, [isSocketReady]);

  const unsubscribeFromComputer = useCallback((computerId) => {
    if (isSocketReady && socketRef.current?.connected && computerId) {
      console.log(`[SocketContext] Emitting frontend:unsubscribe for computer ${computerId}`);
      socketRef.current.emit('frontend:unsubscribe', { computerId });
    } else {
       console.warn(`[SocketContext] Cannot unsubscribe computer ${computerId}: Socket not ready (isSocketReady: ${isSocketReady}, connected: ${socketRef.current?.connected})`);
    }
  }, [isSocketReady]);

  const getComputerStatus = useCallback((computerId) => {
    return computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0, diskUsage: 0, timestamp: 0 };
  }, [computerStatuses]);

  const contextValue = useMemo(() => ({
    socket: socketRef.current,
    isSocketReady,
    subscribeToComputer,
    unsubscribeFromComputer,
    getComputerStatus,
  }), [isSocketReady, subscribeToComputer, unsubscribeFromComputer, getComputerStatus]);

  return (
    <SocketContext.Provider value={contextValue}>
      {children}
    </SocketContext.Provider>
  );
};

export const useSocket = () => {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error('useSocket must be used within a SocketProvider');
  }
  return context;
};
