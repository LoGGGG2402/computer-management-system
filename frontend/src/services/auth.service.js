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
    const response = await api.post(`/auth/login`, {
      username,
      password
    });
    
    if (response.data.data.token) {
      localStorage.setItem('user', JSON.stringify(response.data.data));
    }
    
    return response.data.data;
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
    const response = await api.get(`/auth/me`);
    return response.data.data;
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
      console.error('Error fetching user rooms:', error);
      throw error;
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