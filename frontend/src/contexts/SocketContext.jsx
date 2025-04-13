import React, { createContext, useContext, useEffect, useState, useCallback, useMemo, useRef } from 'react';
import io from 'socket.io-client';
import { useAuth } from './AuthContext';

const SocketContext = createContext(null);

export const SocketProvider = ({ children }) => {
  const socketRef = useRef(null);
  const [computerStatuses, setComputerStatuses] = useState({});
  const { user, isAuthenticated } = useAuth();

  useEffect(() => {
    if (!isAuthenticated || !user?.token) {
       if (socketRef.current) {
           socketRef.current.disconnect();
           socketRef.current = null;
           setComputerStatuses({});
       }
       return;
    }

    if (socketRef.current && socketRef.current.connected) {
        return;
    }

    if (socketRef.current && !socketRef.current.connected) {
        socketRef.current.connect();
     }

    if (!socketRef.current) {
        const newSocket = io(import.meta.env.VITE_API_URL || 'http://localhost:3000', {
          reconnection: true,
          reconnectionAttempts: 5,
          reconnectionDelay: 1000,
          auth: {
            token: user.token
          }
        });
        socketRef.current = newSocket;

        newSocket.on('connect', () => {
          console.log('Socket đã kết nối! ID:', newSocket.id);
        });

        newSocket.on('disconnect', (reason) => {
          console.log('Socket đã ngắt kết nối!', reason);
          if (reason === 'io server disconnect') {
            console.warn('Server đã ngắt kết nối socket.');
          }
        });

        newSocket.on('connect_error', (err) => {
          console.error('Lỗi kết nối socket:', err.message);
          if (err.message === 'Authentication error') {
             console.error('Xác thực Socket thất bại. Token có thể không hợp lệ.');
          }
        });

        newSocket.on('subscribe_response', (data) => {
          if (data.status === 'success') {
            console.log(`Đăng ký thành công computer: ${data.computerId}`);
          } else {
            console.error(`Lỗi đăng ký computer ${data.computerId}: ${data.message}`);
          }
        });

        newSocket.on('unsubscribe_response', (data) => {
          if (data.status === 'success') {
            console.log(`Hủy đăng ký thành công computer: ${data.computerId}`);
            setComputerStatuses(prev => {
                const newState = {...prev};
                delete newState[data.computerId];
                return newState;
            });
          } else {
            console.error(`Lỗi hủy đăng ký computer ${data.computerId}: ${data.message}`);
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
          } else {
              console.warn("Nhận dữ liệu status_updated không hợp lệ:", data);
          }
        });
    }

    return () => {
      if (socketRef.current) {
        socketRef.current.off('connect');
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
  }, [isAuthenticated, user?.token]);

  const subscribeToComputer = useCallback((computerId) => {
    if (!socketRef.current || !socketRef.current.connected || !computerId) {
       console.warn(`Không thể đăng ký computer ${computerId}: Socket chưa sẵn sàng hoặc computerId không hợp lệ.`);
       return;
    }
    socketRef.current.emit('frontend:subscribe', { computerId });
  }, []);

  const unsubscribeFromComputer = useCallback((computerId) => {
    if (!socketRef.current || !socketRef.current.connected || !computerId) {
      console.warn(`Không thể hủy đăng ký computer ${computerId}: Socket chưa sẵn sàng hoặc computerId không hợp lệ.`);
      return;
    }
    socketRef.current.emit('frontend:unsubscribe', { computerId });
  }, []);

  const getComputerStatus = useCallback((computerId) => {
    return computerStatuses[computerId] || { status: 'offline', cpuUsage: 0, ramUsage: 0, diskUsage: 0, timestamp: 0 };
  }, [computerStatuses]);

  const contextValue = useMemo(() => ({
    socket: socketRef.current,
    subscribeToComputer,
    unsubscribeFromComputer,
    getComputerStatus,
  }), [socketRef.current, subscribeToComputer, unsubscribeFromComputer, getComputerStatus]);

  return (
    <SocketContext.Provider value={contextValue}>
      {children}
    </SocketContext.Provider>
  );
};

export const useSocket = () => {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error('useSocket phải được sử dụng bên trong SocketProvider');
  }
  return context;
};
