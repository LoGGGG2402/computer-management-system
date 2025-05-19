/**
 * @fileoverview Authentication Context Provider for managing user authentication state
 *
 * This context manages user authentication state, including login/logout operations,
 * token management, user profile data.
 * It provides functions to verify user authentication.
 *
 * @module AuthContext
 */
import React, {
  createContext,
  useState,
  useContext,
  useEffect,
  useMemo,
  useCallback,
} from "react";
import authService from "../services/auth.service";

const AuthContext = createContext(null);

/**
 * Authentication Provider Component
 *
 * Provides authentication state and operations for the application.
 * Manages user information and token validation.
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
   * Initializes authentication on component mount.
   *
   * This effect:
   * 1. Always attempts to fetch the user profile.
   * 2. Relies on api.js interceptors to handle token refresh if needed.
   * 3. Sets user state upon successful profile retrieval.
   * 4. Handles errors if profile retrieval and subsequent refresh attempts fail.
   */
  useEffect(() => {
    let isMounted = true;

    const initializeAuth = async () => {
      setLoading(true);
      setError(null);
      console.log("initializeAuth");
      try {
        console.log(
          "[AuthContext] Initializing authentication: Attempting to get profile."
        );
        const profileData = await authService.getProfile();

        if (isMounted) {
          if (profileData) {
            const newAccessToken = authService.tokenStorage.getToken();

            console.log(
              "[AuthContext] Profile fetched successfully. Token after getProfile:",
              newAccessToken ? "Exists" : "Null"
            );

            setUser({
              id: profileData.id,
              username: profileData.username,
              role: profileData.role,
              is_active: profileData.is_active,
              token: newAccessToken,
              profile: profileData,
            });

            if (
              authService.userStorage &&
              typeof authService.userStorage.setUserInfo === "function"
            ) {
              const infoToStore = {
                id: profileData.id,
                username: profileData.username,
                role: profileData.role,
                is_active: profileData.is_active,
              };
              authService.userStorage.setUserInfo(infoToStore);
            }
          } else {
            console.log(
              "[AuthContext] getProfile returned null/falsy, setting user to null."
            );
            setUser(null);
          }
        }
      } catch (err) {
        console.error(
          "[AuthContext] Error during initializeAuth (authService.getProfile failed or refresh failed):",
          err.message,
          err
        );
        if (isMounted) {
          setUser(null);
        }
      } finally {
        if (isMounted) {
          setLoading(false);
          console.log("[AuthContext] Initialization complete.");
        }
      }
    };

    initializeAuth();

    return () => {
      isMounted = false;
    };
  }, []);

  /**
   * Authenticates a user with username and password.
   *
   * @async
   * @param {string} username - User's username.
   * @param {string} password - User's password.
   * @returns {Promise<Object>} Promise resolving to the authenticated user data.
   * @throws {Error} When authentication fails.
   */
  const loginAction = useCallback(async (username, password) => {
    setLoading(true);
    setError(null);
    try {
      console.log("[AuthContext] Attempting login...");
      const loggedInData = await authService.login(username, password);
      console.log(
        "[AuthContext] Login successful, attempting to get profile..."
      );
      const profile = await authService.getProfile();

      if (!profile) {
        console.error(
          "[AuthContext] Failed to get profile after successful login."
        );
        await authService.logout();
        throw new Error("Unable to fetch user profile after login.");
      }

      const fullUser = {
        id: loggedInData.id,
        username: loggedInData.username,
        role: loggedInData.role,
        is_active: loggedInData.is_active,
        token: loggedInData.token,
        profile: profile,
      };
      setUser(fullUser);
      setLoading(false);
      console.log("[AuthContext] User set after login:", fullUser);
      return fullUser;
    } catch (err) {
      console.error("[AuthContext] Login failed:", err.message);
      setError(err.message || "Login failed.");
      setUser(null);
      setLoading(false);
      throw err;
    }
  }, []);

  /**
   * Logs out the current user.
   *
   * @async
   */
  const logoutAction = useCallback(async () => {
    setLoading(true);
    console.log("[AuthContext] Attempting logout...");
    try {
      await authService.logout();
      console.log("[AuthContext] Logout successful.");
    } catch (err) {
      console.error(
        "[AuthContext] Error during logout (client-side cleanup will still proceed):",
        err
      );
    } finally {
      setUser(null);
      setError(null);
      setLoading(false);
      console.log("[AuthContext] User state cleared after logout.");
    }
  }, []);

  const authValue = useMemo(
    () => ({
      user,
      loading,
      error,
      isAuthenticated: !!user && !loading && !!user.token,
      isAdmin: user?.role === "admin",
      loginAction,
      logoutAction,
    }),
    [user, loading, error, loginAction, logoutAction]
  );

  return (
    <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>
  );
};

/**
 * Hook for accessing the authentication context.
 *
 * @returns {Object} Authentication context value.
 * @throws {Error} If used outside of an AuthProvider.
 */
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
};
