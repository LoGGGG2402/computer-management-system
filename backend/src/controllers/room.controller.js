const roomService = require('../services/room.service');
const websocketService = require('../services/websocket.service');

/**
 * Controller for room management operations
 */
class RoomController {
  /**
   * Get all rooms with pagination
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
   */
  async updateRoom(req, res) {
    try {
      const id = parseInt(req.params.id);
      const { name, description, layout } = req.body;
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID is required'
        });
      }
      
      const roomData = {};
      
      if (name !== undefined) roomData.name = name;
      if (description !== undefined) roomData.description = description;
      if (layout !== undefined) roomData.layout = layout;
      
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
   * Delete a room
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async deleteRoom(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID is required'
        });
      }
      
      await roomService.deleteRoom(id);
      
      return res.status(200).json({
        status: 'success',
        message: 'Room deleted successfully'
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Room not found'
      });
    }
  }

  /**
   * Assign users to a room
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
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

  /**
   * Send a command to all computers in a room
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleSendCommandToRoom(req, res, next) {
    try {
      const roomId = parseInt(req.params.roomId);
      const { command } = req.body;
      const userId = req.user.id;
      
      if (!roomId || !command) {
        return res.status(400).json({
          status: 'error',
          message: 'Room ID and command are required'
        });
      }
      
      // Send command to all computers in the room
      const sentComputerIds = await websocketService.sendCommandToRoomComputers(roomId, command, userId);
      
      return res.status(202).json({
        status: 'success',
        message: 'Command sent to online agents in room',
        data: { 
          computerIds: sentComputerIds 
        }
      });
    } catch (error) {
      console.error('Send command to room error:', error);
      return res.status(500).json({
        status: 'error',
        message: error.message || 'Failed to send command to room'
      });
    }
  }
}

module.exports = new RoomController();