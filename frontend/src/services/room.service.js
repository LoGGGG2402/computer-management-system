import api from './api';

/**
 * Service for room management operations.
 */
class RoomService {
  /**
   * Get list of all rooms with pagination and filters.
   * Requires JWT token.
   * @param {Object} [filters={}] - Filter parameters.
   * @param {number} [filters.page=1] - Page number.
   * @param {number} [filters.limit=10] - Items per page.
   * @param {string} [filters.name] - Filter by room name (fuzzy search).
   * @param {number} [filters.assigned_user_id] - Filter rooms by assigned user ID.
   * @returns {Promise<Object>} Paginated room data.
   * API Doc Response: { status: "success", data: { total, currentPage, totalPages, rooms: [...] } }
   * @throws {Error} If fetching room list fails.
   */
  async getAllRooms(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', String(filters.page));
      if (filters.limit) params.append('limit', String(filters.limit));
      if (filters.name) params.append('name', filters.name);
      if (filters.assigned_user_id) params.append('assigned_user_id', String(filters.assigned_user_id));
      const response = await api.get(`/rooms?${params.toString()}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch room list.';
      console.error('Error fetching room list:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get detailed room information by ID, including list of computers in the room.
   * Requires JWT token.
   * @param {number|string} id - Room ID.
   * @returns {Promise<Object>} Detailed room data.
   * API Doc Response: { status: "success", data: { id, name, ..., computers: [...] } }
   * @throws {Error} If room not found or error occurs.
   */
  async getRoomById(id) {
    try {
      const response = await api.get(`/rooms/${id}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch room information.';
      console.error('Error fetching room by ID:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Create a new room.
   * Requires admin privileges.
   * @param {Object} roomData - Room data.
   * @param {string} roomData.name - Room name (required).
   * @param {string} [roomData.description] - Room description.
   * @param {Object} roomData.layout - Layout configuration (required).
   * @param {number} roomData.layout.rows - Number of rows (required).
   * @param {number} roomData.layout.columns - Number of columns (required).
   * @returns {Promise<Object>} Created room data.
   * API Doc Response: { status: "success", data: { id, name, ... }, message }
   * @throws {Error} If room creation fails.
   */
  async createRoom(roomData) {
    try {
      const response = await api.post('/rooms', roomData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to create room.';
      console.error('Error creating room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Update room information.
   * Requires admin privileges. Layout cannot be updated via this endpoint according to API doc.
   * @param {number|string} id - ID of room to update.
   * @param {Object} roomData - Room data to update.
   * @param {string} [roomData.name] - New room name.
   * @param {string} [roomData.description] - New room description.
   * @returns {Promise<Object>} Updated room data.
   * API Doc Response: { status: "success", data: { id, name, ... }, message }
   * @throws {Error} If room update fails.
   */
  async updateRoom(id, roomData) {
    try {
      const { layout, ...dataToUpdate } = roomData;
      const response = await api.put(`/rooms/${id}`, dataToUpdate);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update room.';
      console.error('Error updating room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Delete a room. (API Doc does not have room deletion endpoint, needs confirmation)
   * Assumes DELETE /api/rooms/:id endpoint exists and requires admin privileges.
   * @param {number|string} id - ID of room to delete.
   * @returns {Promise<boolean>} True if successful.
   * @throws {Error} If room deletion fails.
   */
  async deleteRoom(id) {
    try {
      await api.delete(`/rooms/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to delete room (endpoint may not exist).';
      console.error('Error deleting room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Assign users to a room.
   * Requires admin privileges.
   * @param {number|string} roomId - Room ID.
   * @param {Array<number|string>} userIds - Array of user IDs.
   * @returns {Promise<Object>} Assignment result.
   * API Doc Response: { status: "success", data: { count: integer }, message }
   * @throws {Error} If user assignment fails.
   */
  async assignUsersToRoom(roomId, userIds) {
    try {
      const response = await api.post(`/rooms/${roomId}/assign`, { userIds });
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to assign users to room.';
      console.error('Error assigning users to room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Unassign users from a room.
   * Requires admin privileges.
   * @param {number|string} roomId - Room ID.
   * @param {Array<number|string>} userIds - Array of user IDs.
   * @returns {Promise<Object>} Unassignment result.
   * API Doc Response: { status: "success", data: { count: integer }, message }
   * @throws {Error} If user unassignment fails.
   */
  async unassignUsersFromRoom(roomId, userIds) {
    try {
      const response = await api.post(`/rooms/${roomId}/unassign`, { userIds });
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to unassign users from room.';
      console.error('Error unassigning users from room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get list of users assigned to a room.
   * Requires admin privileges.
   * @param {number|string} roomId - Room ID.
   * @returns {Promise<Array<Object>>} Array of user objects.
   * API Doc Response: { status: "success", data: { users: [...] } }
   * @throws {Error} If fetching room users fails.
   */
  async getUsersInRoom(roomId) {
    try {
      const response = await api.get(`/rooms/${roomId}/users`);
      return response.data.data.users;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch users in room.';
      console.error('Error fetching users in room:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }
}

export default new RoomService();
