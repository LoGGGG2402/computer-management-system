const userService = require('../services/user.service');

/**
 * Controller for user management operations
 */
class UserController {
  /**
   * Get all users with pagination
   * @param {Object} req - Express request object
   * @param {Object} req.query - Query parameters
   * @param {number} [req.query.page=1] - Page number for pagination
   * @param {number} [req.query.limit=10] - Number of users per page
   * @param {string} [req.query.username] - Filter by username (partial match)
   * @param {string} [req.query.role] - Filter by role (admin/user)
   * @param {boolean|string} [req.query.is_active] - Filter by active status
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Pagination result with:
   *     - total {number} - Total number of users matching criteria
   *     - currentPage {number} - Current page number
   *     - totalPages {number} - Total number of pages
   *     - users {Array<Object>} - Array of user objects:
   *       - id {number} - User ID
   *       - username {string} - Username
   *       - role {string} - User role (admin/user)
   *       - is_active {boolean} - Whether user is active
   *       - created_at {Date} - When user was created
   *       - updated_at {Date} - When user was last updated
   *   - message {string} - Error message (only if status is 'error')
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
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - User ID to retrieve
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - User object with:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {Date} - When user was created
   *     - updated_at {Date} - When user was last updated
   *   - message {string} - Error message (only if status is 'error')
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
   * @param {Object} req.body - Request body
   * @param {string} req.body.username - Username for the new user
   * @param {string} req.body.password - Password for the new user
   * @param {string} [req.body.role='user'] - Role for the new user
   * @param {boolean} [req.body.is_active=true] - Whether the new user is active
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Created user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {Date} - When user was created
   *     - updated_at {Date} - When user was last updated
   *   - message {string} - Success or error message
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
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - User ID to update
   * @param {Object} req.body - Request body
   * @param {string} [req.body.role] - New role for the user
   * @param {boolean} [req.body.is_active] - New active status for the user
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Updated user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {Date} - When user was created
   *     - updated_at {Date} - When user was last updated
   *   - message {string} - Success or error message
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
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - User ID to delete/inactivate
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
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

  /**
   * Reactivate an inactive user
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.id - User ID to reactivate
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Reactivated user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active (true after reactivation)
   *     - created_at {Date} - When user was created
   *     - updated_at {Date} - When user was last updated
   *   - message {string} - Success or error message
   */
  async reactivateUser(req, res) {
    try {
      const id = parseInt(req.params.id);
      
      if (!id) {
        return res.status(400).json({
          status: 'error',
          message: 'User ID is required'
        });
      }
      
      const user = await userService.reactivateUser(id);
      
      return res.status(200).json({
        status: 'success',
        data: user,
        message: 'User reactivated successfully'
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