const roomService = require('../services/room.service');

/**
 * Controller for room management operations
 */
class RoomController {
  /**
   * Get all rooms with pagination
   * @param {Object} req - Express request object
   * @param {Object} req.query - Query parameters
   * @param {number} [req.query.page=1] - Page number for pagination
   * @param {number} [req.query.limit=10] - Number of rooms per page
   * @param {string} [req.query.name] - Filter by room name (partial match)
   * @param {number} [req.query.assigned_user_id] - Filter by assigned user ID
   * @param {Object} req.user - Authenticated user object
   * @param {string} req.user.role - User role ('admin' or 'user')
   * @param {number} req.user.id - User ID
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Pagination result with:
   *     - total {number} - Total number of rooms matching criteria
   *     - currentPage {number} - Current page number
   *     - totalPages {number} - Total number of pages
   *     - rooms {Array<Object>} - Array of room objects:
   *       - id {number} - Room ID
   *       - name {string} - Room name
   *       - description {string} - Room description
   *       - layout {Object} - Room layout configuration
   *         - columns {number} - Number of columns in the room grid
   *         - rows {number} - Number of rows in the room grid
   *       - created_at {Date} - When room was created
   *       - updated_at {Date} - When room was last updated
   *   - message {string} - Error message (only if status is 'error')
   */
  async getAllRooms(req, res) {
    try {
      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 10;
      const name = req.query.name || '';
      const assigned_user_id = req.query.assigned_user_id ? parseInt(req.query.assigned_user_id) : null;
      
      const result = await roomService.getAllRooms(page, limit, name, assigned_user_id, req.user);
      
      return res.status(200).json({
        status: 'success',
        data: result
      });
    } catch (error) {
      return res.status(500).json({
        status: 'error',
        message: error.message || 'Failed to fetch rooms'
      });
    }
  }

  /**
   * Get room by ID with computers
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - Room ID to retrieve
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Room object with:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration
   *       - columns {number} - Number of columns in the room grid
   *       - rows {number} - Number of rows in the room grid
   *     - created_at {Date} - When room was created
   *     - updated_at {Date} - When room was last updated
   *     - computers {Array<Object>} - Array of computers in this room:
   *       - id {number} - Computer ID
   *       - name {string} - Computer name
   *       - status {string} - Computer status ('online'/'offline') 
   *       - has_active_errors {boolean} - Whether computer has active errors
   *       - last_update {Date} - When computer was last updated
   *       - room_id {number} - ID of the room this computer belongs to
   *       - pos_x {number} - X position in room grid
   *       - pos_y {number} - Y position in room grid
   *       - os_info {Object} - Operating system information
   *       - cpu_info {Object} - CPU information
   *       - gpu_info {Object} - GPU information
   *       - total_ram {number} - Total RAM in GB
   *       - total_disk_space {number} - Total disk space in GB
   *   - message {string} - Error message (only if status is 'error')
   */
  async getRoomById(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID is required'
        });
      }
      
      const room = await roomService.getRoomById(id);
      
      return res.status(200).json({
        status: 'success',
        data: room
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Room not found'
      });
    }
  }

  /**
   * Create a new room
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.name - Name for the new room
   * @param {string} [req.body.description] - Description for the new room
   * @param {Object} [req.body.layout] - Layout configuration for the new room
   * @param {number} [req.body.layout.columns] - Number of columns in the room grid
   * @param {number} [req.body.layout.rows] - Number of rows in the room grid
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Created room object:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration
   *       - columns {number} - Number of columns in the room grid
   *       - rows {number} - Number of rows in the room grid
   *     - created_at {Date} - When room was created
   *     - updated_at {Date} - When room was last updated
   *   - message {string} - Success or error message
   */
  async createRoom(req, res) {
    try {
      const { name, description, layout } = req.body;
      
      if (!name) {
        return res.status(400).json({
          status: 'error',
          message: 'Room name is required'
        });
      }
      
      const roomData = {
        name,
        description,
        layout
      };
      
      const room = await roomService.createRoom(roomData);
      
      return res.status(201).json({
        status: 'success',
        data: room,
        message: 'Room created successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to create room'
      });
    }
  }

  /**
   * Update a room
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - Room ID to update
   * @param {Object} req.body - Request body
   * @param {string} [req.body.name] - New name for the room
   * @param {string} [req.body.description] - New description for the room
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Updated room object:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration
   *       - columns {number} - Number of columns in the room grid
   *       - rows {number} - Number of rows in the room grid
   *     - created_at {Date} - When room was created
   *     - updated_at {Date} - When room was last updated
   *   - message {string} - Success or error message
   */
  async updateRoom(req, res) {
    try {
      const id = parseInt(req.params.id);
      const { name, description } = req.body;
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID is required'
        });
      }
      
      const roomData = {};
      
      if (name !== undefined) roomData.name = name;
      if (description !== undefined) roomData.description = description;
      // Layout changes are not allowed for room updates
      
      const room = await roomService.updateRoom(id, roomData);
      
      return res.status(200).json({
        status: 'success',
        data: room,
        message: 'Room updated successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to update room'
      });
    }
  }

  /**
   * Assign users to a room
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.roomId - Room ID to assign users to
   * @param {Object} req.body - Request body
   * @param {number[]} req.body.userIds - Array of user IDs to assign to the room
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Assignment result:
   *     - count {number} - Number of users successfully assigned
   *   - message {string} - Success or error message
   */
  async assignUsersToRoom(req, res) {
    try {
      const roomId = parseInt(req.params.roomId);
      const { userIds } = req.body;
      
      if (!roomId || !userIds || !Array.isArray(userIds)) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID and user IDs array are required'
        });
      }
      
      const count = await roomService.assignUsersToRoom(roomId, userIds);
      
      return res.status(200).json({
        status: 'success',
        data: { count },
        message: `${count} users assigned to room successfully`
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to assign users to room'
      });
    }
  }

  /**
   * Unassign users from a room
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.roomId - Room ID to unassign users from
   * @param {Object} req.body - Request body
   * @param {number[]} req.body.userIds - Array of user IDs to unassign from the room
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Unassignment result:
   *     - count {number} - Number of users successfully unassigned
   *   - message {string} - Success or error message
   */
  async unassignUsersFromRoom(req, res) {
    try {
      const roomId = parseInt(req.params.roomId);
      const { userIds } = req.body;
      
      if (!roomId || !userIds || !Array.isArray(userIds)) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID and user IDs array are required'
        });
      }
      
      const count = await roomService.unassignUsersFromRoom(roomId, userIds);
      
      return res.status(200).json({
        status: 'success',
        data: { count },
        message: `${count} users unassigned from room successfully`
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to unassign users from room'
      });
    }
  }

  /**
   * Get users assigned to a room
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.roomId - Room ID to get assigned users for
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Users result:
   *     - users {Array<Object>} - Array of user objects:
   *       - id {number} - User ID
   *       - username {string} - Username
   *       - role {string} - User role (admin/user)
   *       - is_active {boolean} - Whether user is active
   *       - created_at {Date} - When user was created
   *       - updated_at {Date} - When user was last updated
   *   - message {string} - Error message (only if status is 'error')
   */
  async getUsersInRoom(req, res) {
    try {
      const roomId = parseInt(req.params.roomId);
      
      if (!roomId) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID is required'
        });
      }
      
      const users = await roomService.getUsersInRoom(roomId);
      
      return res.status(200).json({
        status: 'success',
        data: { users }
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Failed to get users in room'
      });
    }
  }
}

module.exports = new RoomController();