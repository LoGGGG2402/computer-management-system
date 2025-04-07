import api from './api';

const computerService = {
  /**
   * Get all computers with pagination and filters
   * @param {number} page - Page number
   * @param {number} limit - Items per page
   * @param {object} filters - Filter parameters object that may include:
   *                          name - Filter by computer name
   *                          roomId - Filter by room ID
   *                          status - Filter by status (online|offline)
   *                          has_errors - Filter to show only computers with errors
   * @returns {Promise} Promise with paginated computers
   */
  getAllComputers: async (page = 1, limit = 10, filters = {}) => {
    try {
      // Build query parameters
      const params = new URLSearchParams();
      params.append('page', page);
      params.append('limit', limit);
      
      // Add filters if provided
      if (filters.name) params.append('name', filters.name);
      if (filters.roomId) params.append('roomId', filters.roomId);
      if (filters.status) params.append('status', filters.status);
      if (filters.has_errors === true) params.append('has_errors', 'true');
      
      const response = await api.get(`/computers?${params.toString()}`);
      return response.data;
    } catch (error) {
      console.error("API Error:", error);
      throw error;
    }
  },

  /**
   * Get computer by ID
   * @param {string} id - Computer ID
   * @returns {Promise} Promise with computer data
   */
  getComputerById: async (id) => {
    try {
      const response = await api.get(`/computers/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Create a new computer
   * @param {object} computerData - Computer data
   * @returns {Promise} Promise with created computer
   */
  createComputer: async (computerData) => {
    try {
      const response = await api.post('/computers', computerData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Update a computer
   * @param {string} id - Computer ID
   * @param {object} computerData - Updated computer data
   * @returns {Promise} Promise with updated computer
   */
  updateComputer: async (id, computerData) => {
    try {
      const response = await api.put(`/computers/${id}`, computerData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Delete a computer
   * @param {string} id - Computer ID
   * @returns {Promise} Promise with deletion status
   */
  deleteComputer: async (id) => {
    try {
      const response = await api.delete(`/computers/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },
};

export default computerService;