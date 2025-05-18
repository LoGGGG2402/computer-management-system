import api from './api';

/**
 * Service for room management operations
 */
class RoomService {
  /**
   * Get all rooms with pagination and filtering
   * @param {Object} filters - Filter parameters
   * @param {number} [filters.page=1] - Page number
   * @param {number} [filters.limit=10] - Items per page
   * @param {string} [filters.name] - Filter by room name (partial match)
   * @param {number} [filters.assigned_user_id] - Filter by assigned user ID
   * @returns {Promise<Object>} Paginated rooms data with:
   *   - total {number} - Total number of rooms matching criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - rooms {Array<Object>} - Array of room objects:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration
   *       - rows {number} - Number of rows in the room grid
   *       - columns {number} - Number of columns in the room grid
   *     - created_at {string} - When room was created (ISO-8601 format)
   *     - updated_at {string} - When room was last updated (ISO-8601 format)
   * @throws {Error} If fetching rooms fails
   */
  async getAllRooms(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', filters.page);
      if (filters.limit) params.append('limit', filters.limit);
      if (filters.name) params.append('name', filters.name);
      if (filters.assigned_user_id) params.append('assigned_user_id', filters.assigned_user_id);
      
      const response = await api.get(`/rooms?${params.toString()}`);
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch rooms';
      console.error('Get rooms error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get room by ID with computers
   * @param {number} id - Room ID to retrieve
   * @returns {Promise<Object>} Room data with:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration
   *     - rows {number} - Number of rows in the room grid
   *     - columns {number} - Number of columns in the room grid
   *   - created_at {string} - When room was created (ISO-8601 format)
   *   - updated_at {string} - When room was last updated (ISO-8601 format)
   *   - computers {Array<Object>} - Array of computers in this room:
   *     - id {number} - Computer ID
   *     - name {string} - Computer name
   *     - status {string} - Computer status ('online'/'offline')
   *     - have_active_errors {boolean} - Whether computer has active errors
   *     - last_update {string} - When computer was last updated (ISO-8601 format)
   *     - room_id {number} - ID of the room this computer belongs to
   *     - pos_x {number} - X position in room grid
   *     - pos_y {number} - Y position in room grid
   *     - os_info {string|Object} - Operating system information (JSON object or serialized string)
   *     - cpu_info {string|Object} - CPU information (JSON object or serialized string)
   *     - gpu_info {string|Object} - GPU information (JSON object or serialized string)
   *     - total_ram {number} - Total RAM in megabytes
   *     - total_disk_space {number} - Total disk space in megabytes
   * @throws {Error} If room is not found
   */
  async getRoomById(id) {
    try {
      const response = await api.get(`/rooms/${id}`);
      if (response.data.status === 'success') {
        const roomData = response.data.data;
        
        // Đảm bảo các trường JSON được phân tích đúng
        if (roomData.computers) {
          roomData.computers = roomData.computers.map(computer => {
            // Parse JSON strings if they are stored as strings
            ['os_info', 'cpu_info', 'gpu_info'].forEach(field => {
              if (typeof computer[field] === 'string') {
                try {
                  computer[field] = JSON.parse(computer[field]);
                } catch {
                  // Giữ nguyên nếu không thể phân tích
                }
              }
            });
            return computer;
          });
        }
        
        return roomData;
      }
      return null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch room';
      console.error('Get room error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Create a new room
   * @param {Object} roomData - Room data
   * @param {string} roomData.name - Name for the new room (3-100 characters)
   * @param {string} [roomData.description] - Description for the new room (0-1000 characters)
   * @param {Object} roomData.layout - Layout configuration for the new room
   * @param {number} roomData.layout.rows - Number of rows in the room grid (1-50)
   * @param {number} roomData.layout.columns - Number of columns in the room grid (1-50)
   * @returns {Promise<Object>} Created room data with:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration
   *     - rows {number} - Number of rows in the room grid
   *     - columns {number} - Number of columns in the room grid
   *   - created_at {string} - When room was created (ISO-8601 format)
   *   - updated_at {string} - When room was last updated (ISO-8601 format)
   * @throws {Error} If creating room fails
   */
  async createRoom(roomData) {
    try {
      const response = await api.post(`/rooms`, roomData);
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to create room';
      console.error('Create room error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Update a room
   * @param {number} id - Room ID to update
   * @param {Object} roomData - Room data to update
   * @param {string} roomData.name - New name for the room (3-100 characters)
   * @param {string} roomData.description - New description for the room (0-1000 characters)
   * @returns {Promise<Object>} Updated room data with:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration (cannot be modified)
   *     - rows {number} - Number of rows in the room grid
   *     - columns {number} - Number of columns in the room grid
   *   - created_at {string} - When room was created (ISO-8601 format)
   *   - updated_at {string} - When room was last updated (ISO-8601 format)
   * @throws {Error} If updating room fails
   */
  async updateRoom(id, roomData) {
    try {
      const response = await api.put(`/rooms/${id}`, roomData);
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update room';
      console.error('Update room error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Delete a room
   * @param {number} id - Room ID to delete
   * @returns {Promise<boolean>} Success status
   * @throws {Error} If deleting room fails
   */
  async deleteRoom(id) {
    try {
      await api.delete(`/rooms/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to delete room';
      console.error('Delete room error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Assign users to a room
   * @param {number} roomId - Room ID to assign users to
   * @param {number[]} userIds - Array of user IDs to assign to the room
   * @returns {Promise<Object>} Assignment result with:
   *   - count {number} - Number of users successfully assigned
   * @throws {Error} If assigning users fails
   */
  async assignUsersToRoom(roomId, userIds) {
    try {
      const response = await api.post(`/rooms/${roomId}/assign`, { userIds });
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to assign users to room';
      console.error('Assign users error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Unassign users from a room
   * @param {number} roomId - Room ID to unassign users from
   * @param {number[]} userIds - Array of user IDs to unassign from the room
   * @returns {Promise<Object>} Unassignment result with:
   *   - count {number} - Number of users successfully unassigned
   * @throws {Error} If unassigning users fails
   */
  async unassignUsersFromRoom(roomId, userIds) {
    try {
      const response = await api.post(`/rooms/${roomId}/unassign`, { userIds });
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to unassign users from room';
      console.error('Unassign users error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get users assigned to a room
   * @param {number} roomId - Room ID to get assigned users for
   * @returns {Promise<Object[]>} Array of user objects:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin'/'user')
   *   - is_active {boolean} - Whether user is active
   *   - created_at {string} - When user was created (ISO-8601 format)
   *   - updated_at {string} - When user was last updated (ISO-8601 format)
   * @throws {Error} If fetching users in room fails
   */
  async getUsersInRoom(roomId) {
    try {
      const response = await api.get(`/rooms/${roomId}/users`);
      return response.data.status === 'success' ? response.data.data.users : [];
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch users in room';
      console.error('Get users in room error:', errorMessage);
      throw new Error(errorMessage);
    }
  }
}

export default new RoomService();