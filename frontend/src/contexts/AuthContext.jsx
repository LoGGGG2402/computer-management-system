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

  /**
   * Initializes authentication on component mount
   * 
   * This effect:
   * 1. Checks for existing user token in storage
   * 2. Validates the token with the server
   * 3. Fetches the user profile if token is valid
   * 4. Handles token validation errors and logout
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
            }
          } catch (err) {
            console.error('Token verification error (possibly invalid/expired):', err);
            authService.logout();
            api.removeAuthToken();
            if (isMounted) {
              setUser(null);
              
            }
          }
        } else {
           if (isMounted) {
             setUser(null);
             
           }
        }
      } catch (err) {
        console.error('Critical authentication initialization error:', err);
        if (isMounted) {
          setError('Authentication failed. Please log in again.');
          authService.logout();
          api.removeAuthToken();
          setUser(null);
          
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
      setLoading(false);
      return fullUser;
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Login failed');
      setUser(null);
      
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
   * 3. Resets any authentication errors
   * 
   * @function
   * @returns {void}
   */
  const logoutAction = useCallback(() => {
    authService.logout();
    api.removeAuthToken();
    setUser(null);
    
    setError(null);
  }, []);

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
     * Function to log in a user
     * @type {function(string, string): Promise<Object>}
     */
    loginAction,
    
    /**
     * Function to log out the current user
     * @type {function(): void}
     */
    logoutAction
  }), [user, loading, error, loginAction, logoutAction]); 

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
