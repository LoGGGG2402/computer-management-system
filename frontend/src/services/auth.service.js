import api from './api';

/**
 * Authentication service for frontend
 */
class AuthService {
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
      
      if (response.data.data.token) {
        localStorage.setItem('user', JSON.stringify(response.data.data));
      }
      
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Login failed. Please check your credentials.';
      console.error('Login error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Logout user by removing stored data
   */
  logout() {
    localStorage.removeItem('user');
  }

  /**
   * Get current user data from local storage
   * @returns {Object|null} User data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin' or 'user')
   *   - is_active {boolean} - Whether the user account is active
   *   - token {string} - JWT authentication token
   *   - expires_at {string} - Token expiration timestamp
   */
  getCurrentUser() {
    const userStr = localStorage.getItem('user');
    if (!userStr) return null;
    
    try {
      return JSON.parse(userStr);
    } catch (error) {
      this.logout();
      return null;
    }
  }

  /**
   * Get user profile from the server
   * @returns {Promise<Object>} User profile data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin' or 'user')
   *   - is_active {boolean} - Whether the user account is active
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} If profile fetch fails
   */
  async getProfile() {
    try {
      const response = await api.get(`/auth/me`);
      return response.data.data;
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
}

export default new AuthService();