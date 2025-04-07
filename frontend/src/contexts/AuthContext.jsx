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
  }, []);

  // Memoized auth value
  const authValue = useMemo(() => ({
    user,
    loading,
    error,
    isAuthenticated: !!user,
    isAdmin: user?.role === 'admin',
    loginAction,
    logoutAction
  }), [user, loading, error, loginAction, logoutAction]);

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