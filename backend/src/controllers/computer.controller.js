const computerService = require('../services/computer.service');
const websocketService = require('../services/websocket.service');
const { v4: uuidv4 } = require('uuid');

/**
 * Controller for computer management operations
 */
class ComputerController {
  /**
   * Get all computers with pagination and filters
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async getAllComputers(req, res) {
    try {
      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 10;
      
      // Extract filters from query params
      const filters = {
        name: req.query.name,
        roomId: req.query.roomId ? parseInt(req.query.roomId) : null,
        status: req.query.status,
        has_errors: req.query.has_errors
      };
      
      const result = await computerService.getAllComputers(page, limit, filters, req.user);
      
      return res.status(200).json({
        status: 'success',
        data: result
      });
    } catch (error) {
      return res.status(500).json({
        status: 'error',
        message: error.message || 'Failed to fetch computers'
      });
    }
  }

  /**
   * Get computer by ID
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async getComputerById(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID is required'
        });
      }
      
      // Access check is now handled by middleware
      const computer = await computerService.getComputerById(id);
      
      return res.status(200).json({
        status: 'success',
        data: computer
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Computer not found'
      });
    }
  }

  /**
   * Update a computer
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async updateComputer(req, res) {
    try {
      const id = parseInt(req.params.id);
      const { name, room_id, pos_x, pos_y } = req.body;
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID is required'
        });
      }
      
      // Prevent updating ip_address
      if (req.body.ip_address !== undefined) {
        return res.status(400).json({
          status: 'error',
          message: 'IP address cannot be updated via this endpoint'
        });
      }
      
      const computerData = {};
      
      if (name !== undefined) computerData.name = name;
      if (room_id !== undefined) computerData.room_id = parseInt(room_id);
      if (pos_x !== undefined) computerData.pos_x = parseInt(pos_x);
      if (pos_y !== undefined) computerData.pos_y = parseInt(pos_y);
      
      const computer = await computerService.updateComputer(id, computerData);
      
      return res.status(200).json({
        status: 'success',
        data: computer,
        message: 'Computer updated successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to update computer'
      });
    }
  }

  /**
   * Delete a computer
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async deleteComputer(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID is required'
        });
      }
      
      await computerService.deleteComputer(id);
      
      return res.status(200).json({
        status: 'success',
        message: 'Computer deleted successfully'
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Computer not found'
      });
    }
  }

  /**
   * Send a command to a computer
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async sendCommand(req, res, next) {
    try {
      const computerId = parseInt(req.params.id);
      const { command } = req.body;
      const userId = req.user.id;
      
      if (!computerId || !command) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID and command are required'
        });
      }
      
      // Generate a unique command ID
      const commandId = uuidv4();
      
      // Store the pending command
      websocketService.storePendingCommand(commandId, userId, computerId);
      
      // Send the command to the agent
      const sent = websocketService.sendCommandToAgent(computerId, command, commandId);
      
      if (sent) {
        return res.status(202).json({
          status: 'success',
          commandId,
          message: 'Command sent to agent'
        });
      } else {
        // If the agent is offline, delete the pending command
        websocketService.pendingCommands.delete(commandId);
        
        return res.status(503).json({
          status: 'error',
          message: 'Agent is offline'
        });
      }
    } catch (error) {
      console.error('Send command error:', error);
      return res.status(500).json({
        status: 'error',
        message: 'Failed to send command'
      });
    }
  }
}

module.exports = new ComputerController();