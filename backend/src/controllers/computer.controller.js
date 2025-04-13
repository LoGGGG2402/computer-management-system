const computerService = require('../services/computer.service');

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
   * Get errors for a specific computer
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async getComputerErrors(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID is required'
        });
      }
      
      const errors = await computerService.getComputerErrors(id);
      
      return res.status(200).json({
        status: 'success',
        data: { errors }
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'Computer not found or no errors available'
      });
    }
  }

  /**
   * Report a new error for a computer
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async reportComputerError(req, res) {
    try {
      const id = parseInt(req.params.id);
      const { error_type, error_message, error_details } = req.body;
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID is required'
        });
      }
      
      if (!error_type || !error_message) {
        return res.status(400).json({
          status: 'error',
          message: 'Error type and message are required'
        });
      }
      
      const errorData = {
        error_type,
        error_message,
        error_details: error_details || {},
        reported_at: new Date(),
        resolved: false
      };
      
      const result = await computerService.reportComputerError(id, errorData);
      
      return res.status(201).json({
        status: 'success',
        data: result,
        message: 'Error reported successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to report error'
      });
    }
  }

  /**
   * Resolve a computer error
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async resolveComputerError(req, res) {
    try {
      const computerId = parseInt(req.params.id);
      const errorId = parseInt(req.params.errorId);
      const { resolution_notes } = req.body;
      
      if (!computerId || !errorId) {
        return res.status(400).json({
          status: 'error',
          message: 'Computer ID and Error ID are required'
        });
      }
      
      const result = await computerService.resolveComputerError(computerId, errorId, resolution_notes);
      
      return res.status(200).json({
        status: 'success',
        data: result,
        message: 'Error resolved successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to resolve error'
      });
    }
  }
}

module.exports = new ComputerController();