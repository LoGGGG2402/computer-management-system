import api from './api';

/**
 * Authentication service for frontend
 */
class AuthService {
  /**
   * Login user and get token
   * @param {string} username - Username
   * @param {string} password - Password
   * @returns {Object} - User data with token
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
   * Logout user
   */
  logout() {
    localStorage.removeItem('user');
  }

  /**
   * Get current user data from local storage
   * @returns {Object|null} - User data or null
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
   * Get user profile
   * @returns {Promise} - User profile data
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
   * Get rooms assigned to the current user
   * @returns {Promise} - User's assigned rooms
   */
  async getUserRooms() {
    try {
      const response = await api.get('/rooms');
      return response.data.data.rooms;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user rooms';
      console.error('Rooms error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Check if user is authenticated
   * @returns {boolean} - Authentication status
   */
  isAuthenticated() {
    return !!this.getCurrentUser()?.token;
  }
}

export default new AuthService();