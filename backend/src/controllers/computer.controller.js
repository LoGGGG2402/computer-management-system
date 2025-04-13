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
}

module.exports = new ComputerController();