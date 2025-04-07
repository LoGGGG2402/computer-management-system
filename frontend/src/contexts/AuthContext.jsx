import { createContext, useState, useContext, useEffect, useMemo, useCallback } from 'react';
import authService from '../services/auth.service';
import api from '../services/api';

// Create Auth Context
const AuthContext = createContext(null);

// Create Provider component
export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  // Add state for user assigned rooms
  const [userRooms, setUserRooms] = useState([]);

  // Initialize auth state on mount
  useEffect(() => {
    const initializeAuth = async () => {
      setLoading(true);
      try {
        const currentUser = authService.getCurrentUser();
        
        if (currentUser) {
          // Set token for API requests
          api.setAuthToken(currentUser.token);
          
          try {
            // Verify token by fetching user profile
            const profile = await authService.getProfile();
            setUser({
              ...currentUser,
              profile
            });
            
            // If user is not admin, fetch assigned rooms
            if (currentUser.role !== 'admin') {
              try {
                const userRoomsData = await authService.getUserRooms();
                setUserRooms(userRoomsData.data || []);
              } catch (err) {
                console.error('Failed to fetch user rooms:', err);
              }
            }
          } catch (err) {
            // If profile fetch fails, token might be invalid
            console.error('Failed to fetch user profile:', err);
            authService.logout();
            setUser(null);
          }
        }
      } catch (err) {
        console.error('Auth initialization error:', err);
        setError('Authentication failed. Please login again.');
      } finally {
        setLoading(false);
      }
    };

    initializeAuth();
  }, []);

  // Login action
  const loginAction = useCallback(async (username, password) => {
    setLoading(true);
    setError(null);
    
    try {
      const userData = await authService.login(username, password);
      
      // Set token for API requests
      api.setAuthToken(userData.token);
      
      // Fetch full profile
      const profile = await authService.getProfile();
      
      setUser({
        ...userData,
        profile
      });
      
      // If user is not admin, fetch assigned rooms
      if (userData.role !== 'admin') {
        try {
          const userRoomsData = await authService.getUserRooms();
          setUserRooms(userRoomsData.data || []);
        } catch (err) {
          console.error('Failed to fetch user rooms:', err);
        }
      }
      
      return userData;
    } catch (err) {
      setError(err.message || 'Login failed');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  // Logout action
  const logoutAction = useCallback(() => {
    authService.logout();
    api.removeAuthToken();
    setUser(null);
    setUserRooms([]);
  }, []);

  // Check if user has access to a specific room
  const hasRoomAccess = useCallback((roomId) => {
    if (!user) return false;
    if (user.role === 'admin') return true;
    return userRooms.some(room => room.id === parseInt(roomId));
  }, [user, userRooms]);

  // Memoized auth value
  const authValue = useMemo(() => ({
    user,
    loading,
    error,
    userRooms,
    isAuthenticated: !!user,
    isAdmin: user?.role === 'admin',
    hasRoomAccess,
    loginAction,
    logoutAction
  }), [user, loading, error, userRooms, hasRoomAccess, loginAction, logoutAction]);

  return (
    <AuthContext.Provider value={authValue}>
      {children}
    </AuthContext.Provider>
  );
};

// Custom hook for using auth context
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export default AuthContext;