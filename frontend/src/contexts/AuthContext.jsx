/**
 * @fileoverview Authentication Context Provider for managing user authentication state
 * 
 * This context manages user authentication state, including login/logout operations,
 * token management, user profile data, and room access permissions.
 * It provides functions to verify user authentication and check access rights
 * to specific rooms in the computer management system.
 * 
 * @module AuthContext
 */
import React, { createContext, useState, useContext, useEffect, useMemo, useCallback } from 'react';
import authService from '../services/auth.service';
import api from '../services/api';

const AuthContext = createContext(null);

/**
 * Authentication Provider Component
 * 
 * Provides authentication state and operations for the application.
 * Manages user information, token validation, and room access permissions.
 * 
 * @component
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components
 * @returns {React.ReactElement} Auth provider component
 */
export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [userRooms, setUserRooms] = useState([]);

  /**
   * Initializes authentication on component mount
   * 
   * This effect:
   * 1. Checks for existing user token in storage
   * 2. Validates the token with the server
   * 3. Fetches the user profile if token is valid
   * 4. Fetches assigned rooms for non-admin users
   * 5. Handles token validation errors and logout
   * 
   * @effect
   */
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
                  console.error('Failed to fetch user rooms:', err);
                }
              } else {
                 if (isMounted) setUserRooms([]);
              }
            }
          } catch (err) {
            console.error('Token verification error (possibly invalid/expired):', err);
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
        console.error('Critical authentication initialization error:', err);
        if (isMounted) {
          setError('Authentication failed. Please log in again.');
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

  /**
   * Authenticates a user with username and password
   * 
   * This function:
   * 1. Sends login credentials to the server
   * 2. Stores the returned token
   * 3. Fetches user profile data
   * 4. For non-admin users, fetches assigned rooms
   * 
   * @function
   * @async
   * @param {string} username - User's username
   * @param {string} password - User's password
   * @returns {Promise<Object>} Promise resolving to the authenticated user data
   * @returns {string} return.id - User ID
   * @returns {string} return.username - Username
   * @returns {string} return.token - JWT authentication token
   * @returns {string} return.role - User role (admin, user, etc.)
   * @returns {Object} return.profile - User profile information
   * @throws {Error} When authentication fails
   */
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
          console.error('Failed to fetch rooms after login:', err);
          setUserRooms([]);
        }
      } else {
        setUserRooms([]);
      }
      setLoading(false);
      return fullUser;
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Login failed');
      setUser(null);
      setUserRooms([]);
      api.removeAuthToken();
      setLoading(false);
      throw err;
    }
  }, []);

  /**
   * Logs out the current user
   * 
   * This function:
   * 1. Clears the authentication token
   * 2. Removes user data from state
   * 3. Clears room access information
   * 4. Resets any authentication errors
   * 
   * @function
   * @returns {void}
   */
  const logoutAction = useCallback(() => {
    authService.logout();
    api.removeAuthToken();
    setUser(null);
    setUserRooms([]);
    setError(null);
  }, []);

  /**
   * Checks if the user has access to a specific room
   * 
   * Access rules:
   * 1. Admin users have access to all rooms
   * 2. Regular users only have access to rooms they are assigned to
   * 
   * @function
   * @param {string|number} roomId - ID of the room to check access for
   * @returns {boolean} True if user has access to the room, false otherwise
   */
  const hasRoomAccess = useCallback((roomId) => {
    const numericRoomId = parseInt(roomId, 10);
    if (isNaN(numericRoomId)) return false;
    if (!user) return false;
    if (user.role === 'admin') return true;
    return userRooms.some(room => room.id === numericRoomId);
  }, [user, userRooms]);

  const authValue = useMemo(() => ({
    /**
     * Current authenticated user information
     * @type {Object|null}
     */
    user,
    
    /**
     * Whether authentication is in progress
     * @type {boolean}
     */
    loading,
    
    /**
     * Any authentication error message
     * @type {string|null}
     */
    error,
    
    /**
     * List of rooms the user has access to (for non-admin users)
     * @type {Array<Object>}
     */
    userRooms,
    
    /**
     * Whether a user is authenticated
     * @type {boolean}
     */
    isAuthenticated: !!user && !loading,
    
    /**
     * Whether the current user is an admin
     * @type {boolean}
     */
    isAdmin: user?.role === 'admin',
    
    /**
     * Function to check if user has access to a room
     * @type {function(string|number): boolean}
     */
    hasRoomAccess,
    
    /**
     * Function to log in a user
     * @type {function(string, string): Promise<Object>}
     */
    loginAction,
    
    /**
     * Function to log out the current user
     * @type {function(): void}
     */
    logoutAction
  }), [user, loading, error, userRooms, hasRoomAccess, loginAction, logoutAction]); 

  return (
      <AuthContext.Provider value={authValue}>
          {children}
      </AuthContext.Provider>
  );
};

/**
 * Hook for accessing the authentication context
 * 
 * @function
 * @returns {Object} Authentication context value
 * @returns {Object|null} return.user - Current authenticated user or null
 * @returns {boolean} return.loading - Whether authentication is in progress
 * @returns {string|null} return.error - Authentication error message if any
 * @returns {Array<Object>} return.userRooms - Rooms the user has access to
 * @returns {boolean} return.isAuthenticated - Whether a user is authenticated
 * @returns {boolean} return.isAdmin - Whether the current user is an admin
 * @returns {function(string|number): boolean} return.hasRoomAccess - Check if user has room access
 * @returns {function(string, string): Promise<Object>} return.loginAction - Log in a user
 * @returns {function(): void} return.logoutAction - Log out the current user
 * @throws {Error} If used outside of an AuthProvider
 */
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
