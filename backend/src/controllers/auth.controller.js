const authService = require('../services/auth.service');

/**
 * Authentication controller for handling login and user verification
 */
class AuthController {
  /**
   * Handle user login
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
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
   * @param {Object} res - Express response object
   */
  async handleGetMe(req, res) {
    try {
      // req.user is set by the verifyToken middleware
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