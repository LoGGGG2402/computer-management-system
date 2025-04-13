import React, { createContext, useContext, useEffect, useState, useCallback, useMemo, useRef } from 'react';
import io from 'socket.io-client';
import { useAuth } from './AuthContext';

const SocketContext = createContext(null);

export const SocketProvider = ({ children }) => {
  const socketRef = useRef(null);
  const [computerStatuses, setComputerStatuses] = useState({});
  const { isAuthenticated, user, logoutAction } = useAuth();

  useEffect(() => {
    if (isAuthenticated && user?.token) {
      if (!socketRef.current || !socketRef.current.connected) {
        if (socketRef.current) {
            socketRef.current.disconnect();
        }

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
          console.log(`Socket connected: ${newSocket.id}`);
          if (isAuthenticated && user?.token) {
            console.log(`Emitting frontend:authenticate...`);
            newSocket.emit('frontend:authenticate', { token: user.token });
          } else {
            console.warn('Cannot authenticate: Invalid auth state post-connect.');
            newSocket.disconnect();
          }
        });

        newSocket.on('auth_response', (data) => {
            if (data?.status === 'success') {
                console.log('Backend auth successful:', data.message);
            } else {
                console.error('Backend auth failed:', data?.message || 'Unknown error');
                logoutAction();
                newSocket.disconnect();
            }
        });

        newSocket.on('disconnect', (reason) => {
          console.log(`Socket disconnected: ${reason}`);
          setComputerStatuses({});
        });

        newSocket.on('connect_error', (err) => {
          console.error(`Socket connection error: ${err.message}`);
        });

        newSocket.on('subscribe_response', (data) => {
          console.log('Subscribe response:', data);
        });

        newSocket.on('unsubscribe_response', (data) => {
           console.log('Unsubscribe response:', data);
           if (data.status === 'success' && data.computerId) {
                setComputerStatuses(prev => {
                    const newState = {...prev};
                    delete newState[data.computerId];
                    return newState;
                });
           }
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
      }
    } else {
      if (socketRef.current && socketRef.current.connected) {
        console.log('Disconnecting socket due to auth state change.');
        socketRef.current.disconnect();
        socketRef.current = null;
        setComputerStatuses({});
      }
    }

    return () => {
      if (socketRef.current) {
        console.log('Cleaning up socket...');
        socketRef.current.off('connect');
        socketRef.current.off('auth_response');
        socketRef.current.off('disconnect');
        socketRef.current.off('connect_error');
        socketRef.current.off('subscribe_response');
        socketRef.current.off('unsubscribe_response');
        socketRef.current.off('computer:status_updated');
        socketRef.current.disconnect();
        socketRef.current = null;
        setComputerStatuses({});
      }
    };
  }, [isAuthenticated, user?.token, logoutAction]);

  const subscribeToComputer = useCallback((computerId) => {
    if (socketRef.current?.connected && computerId) {
       socketRef.current.emit('frontend:subscribe', { computerId });
    } else {
       console.warn(`Cannot subscribe computer ${computerId}: Socket not ready or not authenticated.`);
    }
  }, []);

  const unsubscribeFromComputer = useCallback((computerId) => {
    if (socketRef.current?.connected && computerId) {
      socketRef.current.emit('frontend:unsubscribe', { computerId });
    } else {
       console.warn(`Cannot unsubscribe computer ${computerId}: Socket not ready.`);
    }
  }, []);

  const getComputerStatus = useCallback((computerId) => {
    return computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0, diskUsage: 0, timestamp: 0 };
  }, [computerStatuses]);

  const contextValue = useMemo(() => ({
    socket: socketRef.current,
    subscribeToComputer,
    unsubscribeFromComputer,
    getComputerStatus,
  }), [subscribeToComputer, unsubscribeFromComputer, getComputerStatus]);

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
