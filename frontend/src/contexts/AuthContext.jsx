import React, { createContext, useState, useContext, useEffect, useMemo, useCallback } from 'react';
import authService from '../services/auth.service';
import api from '../services/api';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [userRooms, setUserRooms] = useState([]);

  useEffect(() => {
    let isMounted = true;

    const initializeAuth = async () => {
      setError(null);
      try {
        const currentUser = authService.getCurrentUser();
        if (currentUser && currentUser.token) {
          api.setAuthToken(currentUser.token);
          try {
            const profile = await authService.getProfile();
            if (isMounted) {
              setUser({ ...currentUser, profile });
              if (currentUser.role !== 'admin') {
                try {
                  const userRoomsData = await authService.getUserRooms();
                  if (isMounted) {
                    setUserRooms(userRoomsData.data || []);
                  }
                } catch (err) {
                  console.error('Không thể lấy danh sách phòng của người dùng:', err);
                }
              } else {
                 if (isMounted) setUserRooms([]);
              }
            }
          } catch (err) {
            console.error('Lỗi xác minh token (có thể không hợp lệ/hết hạn):', err);
            authService.logout();
            api.removeAuthToken();
            if (isMounted) {
              setUser(null);
              setUserRooms([]);
            }
          }
        } else {
           if (isMounted) {
             setUser(null);
             setUserRooms([]);
           }
        }
      } catch (err) {
        console.error('Lỗi khởi tạo xác thực nghiêm trọng:', err);
        if (isMounted) {
          setError('Xác thực thất bại. Vui lòng đăng nhập lại.');
          authService.logout();
          api.removeAuthToken();
          setUser(null);
          setUserRooms([]);
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    initializeAuth();

    return () => {
      isMounted = false;
    };
  }, []);

  const loginAction = useCallback(async (username, password) => {
    setLoading(true);
    setError(null);
    try {
      const userData = await authService.login(username, password);
      api.setAuthToken(userData.token);
      const profile = await authService.getProfile();
      const fullUser = { ...userData, profile };
      setUser(fullUser);

      if (userData.role !== 'admin') {
        try {
          const userRoomsData = await authService.getUserRooms();
          setUserRooms(userRoomsData.data || []);
        } catch (err) {
          console.error('Không thể lấy danh sách phòng sau khi đăng nhập:', err);
          setUserRooms([]);
        }
      } else {
        setUserRooms([]);
      }
      setLoading(false);
      return fullUser;
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Đăng nhập thất bại');
      setUser(null);
      setUserRooms([]);
      api.removeAuthToken();
      setLoading(false);
      throw err;
    }
  }, []);

  const logoutAction = useCallback(() => {
    authService.logout();
    api.removeAuthToken();
    setUser(null);
    setUserRooms([]);
    setError(null);
  }, []);

  const hasRoomAccess = useCallback((roomId) => {
    const numericRoomId = parseInt(roomId, 10);
    if (isNaN(numericRoomId)) return false;
    if (!user) return false;
    if (user.role === 'admin') return true;
    return userRooms.some(room => room.id === numericRoomId);
  }, [user, userRooms]);

  const authValue = useMemo(() => ({
    user,
    loading,
    error,
    userRooms,
    isAuthenticated: !!user && !loading,
    isAdmin: user?.role === 'admin',
    hasRoomAccess,
    loginAction,
    logoutAction
  }), [user, loading, loginAction, logoutAction]); // Removed error, userRooms, hasRoomAccess

  return (
      <AuthContext.Provider value={authValue}>
          {children}
      </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
