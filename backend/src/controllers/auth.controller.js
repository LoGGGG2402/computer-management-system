const authService = require('../services/auth.service');

/**
 * Authentication controller for handling login and user verification
 */
class AuthController {
  /**
   * Handle user login
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.username - Username for authentication
   * @param {string} req.body.password - Password for authentication
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - User authentication data (only if status is 'success'):
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role ('admin' or 'user')
   *     - is_active {boolean} - Whether the user account is active
   *     - token {string} - JWT authentication token
   *     - expires_at {string} - Token expiration timestamp
   *   - message {string} - Error message (only if status is 'error')
   */
  async handleLogin(req, res) {
    try {
      const { username, password } = req.body;
      
      if (!username || !password) {
        return res.status(400).json({ 
          status: 'error', 
          message: 'Username and password are required' 
        });
      }
      
      const userData = await authService.login(username, password);
      
      return res.status(200).json({
        status: 'success',
        data: userData
      });
    } catch (error) {
      return res.status(401).json({ 
        status: 'error', 
        message: error.message || 'Authentication failed' 
      });
    }
  }

  /**
   * Get current authenticated user details
   * @param {Object} req - Express request object
   * @param {Object} req.user - User object set by the verifyToken middleware
   * @param {number} req.user.id - ID of the authenticated user
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - User data (only if status is 'success'):
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role ('admin' or 'user')
   *     - is_active {boolean} - Whether the user account is active
   *     - created_at {Date} - When the user was created
   *     - updated_at {Date} - When the user was last updated
   *   - message {string} - Error message (only if status is 'error')
   */
  async handleGetMe(req, res) {
    try {
      const userData = await authService.getUserById(req.user.id);
      return res.status(200).json({
        status: 'success',
        data: userData
      });
    } catch (error) {
      return res.status(404).json({ 
        status: 'error', 
        message: error.message || 'User not found' 
      });
    }
  }
}

module.exports = new AuthController();