import React, { createContext, useState, useContext, useEffect, useMemo, useCallback } from 'react';
import authService from '../services/auth.service';
import api from '../services/api';

// Tạo Auth Context
const AuthContext = createContext(null);

// Tạo Provider component
export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  // Thêm state cho các phòng được gán cho người dùng
  const [userRooms, setUserRooms] = useState([]);

  // Khởi tạo trạng thái xác thực khi component mount
  useEffect(() => {
    const initializeAuth = async () => {
      setLoading(true);
      setError(null); // Reset lỗi khi bắt đầu
      try {
        const currentUser = authService.getCurrentUser();

        if (currentUser && currentUser.token) {
          // Đặt token cho các yêu cầu API
          api.setAuthToken(currentUser.token);

          try {
            // Xác minh token bằng cách lấy thông tin hồ sơ người dùng
            const profile = await authService.getProfile();
            setUser({
              ...currentUser,
              profile // Lưu trữ thông tin hồ sơ đầy đủ
            });

            // Nếu người dùng không phải là admin, lấy danh sách phòng được gán
            if (currentUser.role !== 'admin') {
              try {
                const userRoomsData = await authService.getUserRooms();
                setUserRooms(userRoomsData.data || []);
              } catch (err) {
                console.error('Không thể lấy danh sách phòng của người dùng:', err);
                // Có thể xử lý lỗi này một cách nhẹ nhàng hơn, không nhất thiết phải đăng xuất
              }
            }
          } catch (err) {
            // Nếu lấy hồ sơ thất bại, token có thể không hợp lệ hoặc hết hạn
            console.error('Không thể lấy hồ sơ người dùng (token có thể không hợp lệ):', err);
            authService.logout(); // Đăng xuất người dùng
            api.removeAuthToken(); // Xóa token khỏi API header
            setUser(null);
            setUserRooms([]); // Xóa danh sách phòng
          }
        } else {
           // Nếu không có currentUser hoặc token, đảm bảo trạng thái được reset
           setUser(null);
           setUserRooms([]);
        }
      } catch (err) {
        console.error('Lỗi khởi tạo xác thực:', err);
        setError('Xác thực thất bại. Vui lòng đăng nhập lại.');
        // Đảm bảo trạng thái được reset khi có lỗi nghiêm trọng
        authService.logout();
        api.removeAuthToken();
        setUser(null);
        setUserRooms([]);
      } finally {
        setLoading(false);
      }
    };

    initializeAuth();
    // Không có dependencies vì chỉ chạy một lần khi mount
  }, []);

  // Hành động đăng nhập - sử dụng useCallback
  const loginAction = useCallback(async (username, password) => {
    setLoading(true);
    setError(null);

    try {
      const userData = await authService.login(username, password);

      // Đặt token cho các yêu cầu API
      api.setAuthToken(userData.token);

      // Lấy hồ sơ đầy đủ
      const profile = await authService.getProfile();

      const fullUser = {
        ...userData,
        profile
      };
      setUser(fullUser);

      // Nếu người dùng không phải là admin, lấy danh sách phòng được gán
      if (userData.role !== 'admin') {
        try {
          const userRoomsData = await authService.getUserRooms();
          setUserRooms(userRoomsData.data || []);
        } catch (err) {
          console.error('Không thể lấy danh sách phòng của người dùng sau khi đăng nhập:', err);
           setUserRooms([]); // Đặt lại nếu có lỗi
        }
      } else {
        setUserRooms([]); // Admin không có phòng cụ thể, reset state
      }

      return fullUser; // Trả về thông tin người dùng đầy đủ
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Đăng nhập thất bại');
      // Đảm bảo trạng thái được reset khi đăng nhập thất bại
      setUser(null);
      setUserRooms([]);
      api.removeAuthToken();
      throw err; // Ném lỗi để component gọi có thể xử lý
    } finally {
      setLoading(false);
    }
  }, []); // Không có dependencies vì không phụ thuộc state/props bên ngoài

  // Hành động đăng xuất - sử dụng useCallback
  const logoutAction = useCallback(() => {
    authService.logout();
    api.removeAuthToken();
    setUser(null);
    setUserRooms([]);
    setError(null); // Reset lỗi khi đăng xuất
  }, []); // Không có dependencies

  // Kiểm tra quyền truy cập phòng - sử dụng useCallback
  const hasRoomAccess = useCallback((roomId) => {
    // Chuyển đổi roomId sang số nếu cần thiết và hợp lệ
    const numericRoomId = parseInt(roomId, 10);
    if (isNaN(numericRoomId)) return false; // ID phòng không hợp lệ

    if (!user) return false;
    if (user.role === 'admin') return true; // Admin có quyền truy cập mọi phòng

    // Kiểm tra xem numericRoomId có trong danh sách userRooms không
    return userRooms.some(room => room.id === numericRoomId);
  }, [user, userRooms]); // Phụ thuộc vào user và userRooms

  // Giá trị context được memoized bằng useMemo
  const authValue = useMemo(() => ({
    user,
    loading,
    error,
    userRooms, // Vẫn cung cấp userRooms nếu cần thiết ở nơi khác
    isAuthenticated: !!user,
    isAdmin: user?.role === 'admin',
    hasRoomAccess,
    loginAction,
    logoutAction
  }), [user, loading, error, userRooms, hasRoomAccess, loginAction, logoutAction]); // Dependencies đầy đủ

  return (
    <AuthContext.Provider value={authValue}>
      {children}
    </AuthContext.Provider>
  );
};

// Custom hook để sử dụng auth context
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth phải được sử dụng bên trong AuthProvider');
  }
  return context;
};