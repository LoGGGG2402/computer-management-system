const userService = require('../services/user.service');

/**
 * Controller for user management operations
 */
class UserController {
  /**
   * Get all users with pagination
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async getAllUsers(req, res) {
    try {
      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 10;
      const search = req.query.username || '';
      const role = req.query.role || null;
      const is_active = req.query.is_active !== undefined ? req.query.is_active : null;
      
      const result = await userService.getAllUsers(page, limit, search, role, is_active);
      
      return res.status(200).json({
        status: 'success',
        data: result
      });
    } catch (error) {
      return res.status(500).json({
        status: 'error',
        message: error.message || 'Failed to fetch users'
      });
    }
  }

  /**
   * Get user by ID
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async getUserById(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'User ID is required'
        });
      }
      
      const user = await userService.getUserById(id);

      console.log('Fetched User:', user);
      
      return res.status(200).json({
        status: 'success',
        data: user
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'User not found'
      });
    }
  }

  /**
   * Create a new user
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async createUser(req, res) {
    try {
      const { username, password, role, is_active } = req.body;
      
      if (!username || !password) {
        return res.status(400).json({
          status: 'error',
          message: 'Username and password are required'
        });
      }
      
      const userData = {
        username,
        password,
        role,
        is_active
      };
      
      const user = await userService.createUser(userData);
      
      return res.status(201).json({
        status: 'success',
        data: user,
        message: 'User created successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to create user'
      });
    }
  }

  /**
   * Update a user
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async updateUser(req, res) {
    try {
      const id = parseInt(req.params.id);
      const { role, is_active } = req.body;
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'User ID is required'
        });
      }
      
      // Username and password cannot be updated via this endpoint
      if (req.body.username || req.body.password) {
        return res.status(400).json({
          status: 'error',
          message: 'Username and password cannot be updated via this endpoint'
        });
      }
      
      const userData = {};
      
      if (role !== undefined) userData.role = role;
      if (is_active !== undefined) userData.is_active = is_active;
      
      const user = await userService.updateUser(id, userData);
      
      return res.status(200).json({
        status: 'success',
        data: user,
        message: 'User updated successfully'
      });
    } catch (error) {
      return res.status(400).json({
        status: 'error',
        message: error.message || 'Failed to update user'
      });
    }
  }

  /**
   * Delete/inactivate a user
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   */
  async deleteUser(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'User ID is required'
        });
      }
      
      await userService.deleteUser(id);
      
      return res.status(200).json({
        status: 'success',
        message: 'User inactivated successfully'
      });
    } catch (error) {
      return res.status(404).json({
        status: 'error',
        message: error.message || 'User not found'
      });
    }
  }
}

module.exports = new UserController();