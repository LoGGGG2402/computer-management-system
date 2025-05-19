import api from './api';

/**
 * Store access token in memory.
 * JWT access token should not be stored in localStorage for security reasons.
 */
const tokenStorage = {
  token: null,
  setToken(token) {
    this.token = token;
  },
  getToken() {
    return this.token;
  },
  clearToken() {
    this.token = null;
  },
};

/**
 * Authentication service.
 * Handles login, logout, token refresh, and user information management.
 */
class AuthService {
  /**
   * Getter for tokenStorage to be accessed by api.js.
   */
  get tokenStorage() {
    return tokenStorage;
  }

  /**
   * User login.
   * @param {string} username - Username.
   * @param {string} password - Password.
   * @returns {Promise<Object>} User data including token.
   * @throws {Error} If login fails.
   * Successful response structure from API doc:
   * {
   * "status": "success",
   * "data": {
   * "id": "integer",
   * "username": "string",
   * "role": "string ('admin' or 'user')",
   * "is_active": "boolean",
   * "token": "string (JWT Access Token)",
   * "expires_at": "string (ISO-8601 datetime)"
   * }
   * }
   * Cookie `refreshToken` is set as HttpOnly by server.
   */
  async login(username, password) {
    try {
      const response = await api.post('/auth/login', { username, password });
      if (response.data.status === 'success' && response.data.data.token) {
        const userData = response.data.data;
        tokenStorage.setToken(userData.token);
        return userData;
      }
      throw new Error(response.data.message || 'Login unsuccessful.');
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Login failed. Please check your credentials.';
      console.error('Login error:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Refresh access token.
   * API endpoint `/api/auth/refresh-token` uses HttpOnly refreshToken cookie.
   * @returns {Promise<Object>} User data with new access token.
   * Successful response structure from API doc:
   * {
   * "status": "success",
   * "data": {
   * "token": "string (new JWT Access Token)",
   * "expires_at": "string (ISO-8601 datetime)"
   * }
   * }
   * New `refreshToken` cookie is also set HttpOnly by server (token rotation).
   * @throws {Error} If token refresh fails.
   */
  async refreshToken() {
    try {
      const response = await api.post('/auth/refresh-token');
      if (response.data.status === 'success' && response.data.data.token) {
        const { token, expires_at } = response.data.data;
        tokenStorage.setToken(token);
        return { token, expires_at };
      }
      throw new Error(response.data.message || 'Token refresh unsuccessful.');
    } catch (error) {
      console.error('Token refresh error:', error.extractedMessage || error.message, error);
      throw error;
    }
  }

  /**
   * User logout.
   * Calls `/api/auth/logout` API to invalidate refresh token on server.
   * Clears token and user information on client side.
   */
  async logout() {
    try {
      await api.post('/auth/logout');
    } catch (error) {
      console.error('Server-side logout error (can be ignored if client cleanup is done):', error.extractedMessage || error.message);
    } finally {
      tokenStorage.clearToken();
    }
  }

  /**
   * Handle refresh token error (e.g., expired or invalid refresh token).
   * This function will logout the user and redirect to login page.
   */
  async handleRefreshTokenError() {
    console.warn('Token refresh error. Logging out user.');
    await this.logout();
    if (window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
  }

  /**
   * Get current user profile information.
   * API endpoint: `/api/auth/me`
   * @returns {Promise<Object|null>} User profile data or null if failed.
   * Successful response structure from API doc:
   * {
   * "status": "success",
   * "data": {
   * "id": "integer",
   * "username": "string",
   * "role": "string",
   * "is_active": "boolean",
   * "created_at": "string (ISO-8601 datetime)",
   * "updated_at": "string (ISO-8601 datetime)"
   * }
   * }
   */
  async getProfile() {
    try {
      const response = await api.get('/auth/me');
      if (response.data.status === 'success') {
        return response.data.data;
      }
      return null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user profile information.';
      console.error('Profile fetch error:', errorMessage, error);
      return null;
    }
  }
}

export default new AuthService();
