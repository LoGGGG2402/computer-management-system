import api from './api';

/**
 * In-memory token storage that doesn't use localStorage
 * This prevents XSS vulnerabilities from accessing tokens via JavaScript
 */
const tokenStorage = {
  user: null,
  
  setUser(userData) {
    this.user = userData;
  },
  
  getUser() {
    return this.user;
  },
  
  clearUser() {
    this.user = null;
  }
};

/**
 * Authentication service for frontend
 */
class AuthService {
  /**
   * Token storage accessor
   */
  get tokenStorage() {
    return tokenStorage;
  }
  /**
   * Refresh the access token using HTTP-only refresh token cookie
   * @returns {Promise<Object>} Updated user data with new access token
   * @throws {Error} If token refresh fails
   */
  async refreshToken() {
    try {
      const response = await api.post(`/auth/refresh-token`);
      
      if (response.data.status === 'success' && response.data.data.token) {
        // Get current user data
        const currentUser = this.getCurrentUser();
        
        // Update the token and expiry
        const updatedUser = {
          ...currentUser,
          token: response.data.data.token,
          expires_at: response.data.data.expires_at
        };
        
        // Store updated user data in memory (not localStorage)
        tokenStorage.setUser(updatedUser);
        
        // Update API headers with the new token
        api.setAuthToken(updatedUser.token);
        
        return updatedUser;
      }
      
      return null;
    } catch (error) {
      console.error('Token refresh failed:', error);
      // Remove token from API headers
      api.removeAuthToken();
      // Clear user data if refresh fails
      this.logout();
      throw new Error('Session expired. Please login again.');
    }
  }
  /**
   * Login user and get token
   * @param {string} username - Username for authentication
   * @param {string} password - Password for authentication
   * @returns {Promise<Object>} User data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin' or 'user')
   *   - is_active {boolean} - Whether the user account is active
   *   - token {string} - JWT authentication token
   *   - expires_at {string} - Token expiration timestamp
   * @throws {Error} If login fails
   */
  async login(username, password) {
    try {
      const response = await api.post(`/auth/login`, {
        username,
        password
      });
      
      if (response.data.status === 'success' && response.data.data.token) {
        // Store user data in memory, not localStorage
        tokenStorage.setUser(response.data.data);
        
        // Set token for API requests
        api.setAuthToken(response.data.data.token);
      }
      
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Login failed. Please check your credentials.';
      console.error('Login error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Logout user by invalidating server tokens and removing stored data
   * @returns {Promise<void>}
   */
  async logout() {
    try {
      // Call the server to invalidate refresh token
      await api.post(`/auth/logout`);
    } catch (error) {
      console.error('Error during server logout:', error);
    } finally {
      // Always remove in-memory data regardless of server response
      tokenStorage.clearUser();
      // Remove token from API headers
      api.removeAuthToken();
    }
  }

  /**
   * Get current user data from memory storage
   * @returns {Object|null} User data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin' or 'user')
   *   - is_active {boolean} - Whether the user account is active
   *   - token {string} - JWT authentication token
   *   - expires_at {string} - Token expiration timestamp
   */
  getCurrentUser() {
    return tokenStorage.getUser();
  }

  /**
   * Get user profile from the server
   * @returns {Promise<Object>} User profile data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin' or 'user')
   *   - is_active {boolean} - Whether the user account is active
   *   - created_at {string} - When the user was created (ISO-8601 format)
   *   - updated_at {string} - When the user was last updated (ISO-8601 format)
   * @throws {Error} If profile fetch fails
   */
  async getProfile() {
    try {
      const response = await api.get(`/auth/me`);
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user profile';
      console.error('Profile error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Check if user is authenticated
   * @returns {boolean} Authentication status
   */
  isAuthenticated() {
    return !!this.getCurrentUser()?.token;
  }
  
  /**
   * Check if the access token is about to expire
   * @param {number} bufferSeconds - Number of seconds before expiry to consider it "about to expire"
   * @returns {boolean} Whether the token is about to expire
   */
  isTokenExpiringSoon(bufferSeconds = 60) {
    const user = this.getCurrentUser();
    if (!user || !user.expires_at) return true;
    
    try {
      const expiryTime = new Date(user.expires_at).getTime();
      const currentTime = Date.now();
      
      // Consider token expiring if within buffer time
      return expiryTime - currentTime < bufferSeconds * 1000;
    } catch (error) {
      console.error('Error checking token expiry:', error);
      return true;
    }
  }
}

export default new AuthService();