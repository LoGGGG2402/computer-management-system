import api from './api';

const userService = {
  /**
   * Get all users with pagination and filters
   * @param {number} page - Page number
   * @param {number} limit - Items per page
   * @param {string} username - Filter by username
   * @param {string} role - Filter by role (admin|user)
   * @param {boolean} is_active - Filter by active status
   * @returns {Promise} Promise with paginated users
   */
  getAllUsers: async (page = 1, limit = 10, username = '', role = null, is_active = null) => {
    try {
      // Build query parameters
      const params = new URLSearchParams();
      params.append('page', page);
      params.append('limit', limit);
      
      if (username) params.append('username', username);
      if (role) params.append('role', role);
      if (is_active !== null) params.append('is_active', is_active);
      
      const response = await api.get(`/users?${params.toString()}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Get user by ID
   * @param {string} id - User ID
   * @returns {Promise} Promise with user data
   */
  getUserById: async (id) => {
    try {
      const response = await api.get(`/users/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Create a new user
   * @param {object} userData - User data
   * @returns {Promise} Promise with created user
   */
  createUser: async (userData) => {
    try {
      const response = await api.post('/users', userData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Update a user
   * @param {string} id - User ID
   * @param {object} userData - Updated user data
   * @returns {Promise} Promise with updated user
   */
  updateUser: async (id, userData) => {
    try {
      const response = await api.put(`/users/${id}`, userData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Delete a user
   * @param {string} id - User ID
   * @returns {Promise} Promise with deletion status
   */
  deleteUser: async (id) => {
    try {
      const response = await api.delete(`/users/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Get rooms assigned to a user
   * @param {string} userId - User ID
   * @returns {Promise} Promise with user's rooms
   */
  getUserRooms: async (userId) => {
    try {
      const response = await api.get(`/users/${userId}/rooms`);
      return response.data;
    } catch (error) {
      throw error;
    }
  }
};

export default userService;