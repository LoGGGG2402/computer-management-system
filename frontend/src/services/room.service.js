import api from './api';

const roomService = {
  /**
   * Get all rooms with pagination and filters
   * @param {number} page - Page number
   * @param {number} limit - Items per page
   * @param {string} name - Filter by room name
   * @param {number} assigned_user_id - Filter by assigned user ID
   * @returns {Promise} Promise with paginated rooms
   */
  getAllRooms: async (page = 1, limit = 10, name = '', assigned_user_id = null) => {
    try {
      // Build query parameters
      const params = new URLSearchParams();
      params.append('page', page);
      params.append('limit', limit);
      
      if (name) params.append('name', name);
      if (assigned_user_id) params.append('assigned_user_id', assigned_user_id);
      
      const response = await api.get(`/rooms?${params.toString()}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Get room by ID
   * @param {string} id - Room ID
   * @returns {Promise} Promise with room data
   */
  getRoomById: async (id) => {
    try {
      const response = await api.get(`/rooms/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Create a new room
   * @param {object} roomData - Room data
   * @returns {Promise} Promise with created room
   */
  createRoom: async (roomData) => {
    try {
      const response = await api.post('/rooms', roomData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Update a room
   * @param {string} id - Room ID
   * @param {object} roomData - Updated room data
   * @returns {Promise} Promise with updated room
   */
  updateRoom: async (id, roomData) => {
    try {
      const response = await api.put(`/rooms/${id}`, roomData);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Delete a room
   * @param {string} id - Room ID
   * @returns {Promise} Promise with deletion status
   */
  deleteRoom: async (id) => {
    try {
      const response = await api.delete(`/rooms/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Assign users to a room
   * @param {string} roomId - Room ID
   * @param {Array} userIds - Array of user IDs to assign
   * @returns {Promise} Promise with assignment status
   */
  assignUsersToRoom: async (roomId, userIds) => {
    try {
      const response = await api.post(`/rooms/${roomId}/assign`, { userIds });
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Unassign users from a room
   * @param {string} roomId - Room ID
   * @param {Array} userIds - Array of user IDs to unassign
   * @returns {Promise} Promise with unassignment status
   */
  unassignUsersFromRoom: async (roomId, userIds) => {
    try {
      const response = await api.post(`/rooms/${roomId}/unassign`, { userIds });
      return response.data;
    } catch (error) {
      throw error;
    }
  },

  /**
   * Get users assigned to a room
   * @param {string} roomId - Room ID
   * @returns {Promise} Promise with room's users
   */
  getUsersInRoom: async (roomId) => {
    try {
      const response = await api.get(`/rooms/${roomId}/users`);
      return response.data;
    } catch (error) {
      throw error;
    }
  }
};

export default roomService;