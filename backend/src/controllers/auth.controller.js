const authService = require('../services/auth.service');
const authConfig = require('../config/auth.config');
const logger = require('../utils/logger');

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
   *     - expires_at {string} - Token expiration timestamp (ISO-8601)
   *   - message {string} - Error message (only if status is 'error')
   */
  async handleLogin(req, res) {
    try {
      const { username, password } = req.body;
      
      if (!username || !password) {
        logger.debug('Login attempt with missing credentials', { 
          hasUsername: !!username, 
          hasPassword: !!password,
          ip: req.ip
        });
        
        return res.status(400).json({ 
          status: 'error', 
          message: 'Username and password are required' 
        });
      }
      
      // Additional validation for username and password format
      const usernameRegex = /^[a-zA-Z][a-zA-Z0-9_-]{2,49}$/;
      if (typeof username !== 'string' || !usernameRegex.test(username)) {
        logger.warn('Invalid username format during login attempt', { username, ip: req.ip });
        return res.status(400).json({
          status: 'error',
          message: 'Invalid username format. Must be 3-50 chars, alphanumeric with _-, start with a letter.'
        });
      }

      const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,128}$/;
      if (typeof password !== 'string' || !passwordRegex.test(password)) {
        logger.warn('Invalid password format during login attempt', { username, ip: req.ip });
        return res.status(400).json({
          status: 'error',
          message: 'Invalid password format. Must be 8-128 chars, with uppercase, lowercase, number, and special char.'
        });
      }
      
      logger.debug(`Login attempt for username: ${username}`, { ip: req.ip });
      const userData = await authService.login(username, password);
      
      logger.info(`Successful login: ${userData.username} (ID: ${userData.id}, Role: ${userData.role})`, { 
        userId: userData.id,
        ip: req.ip
      });
      
      // Set refresh token as HttpOnly cookie
      const cookieOptions = {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production', // Only send over HTTPS in production
        sameSite: 'Strict',
        maxAge: authConfig.refreshToken.cookieMaxAge,
        path: '/'
      };
      
      res.cookie('refreshToken', userData.refreshToken, cookieOptions);
      
      // Remove the refresh token from the response body for security
      const responseData = { ...userData };
      delete responseData.refreshToken;
      
      return res.status(200).json({
        status: 'success',
        data: responseData
      });
    } catch (error) {
      logger.warn('Failed login attempt', { 
        username: req.body.username,
        error: error.message,
        ip: req.ip
      });
      
      // Handle specific error cases
      if (error.message === 'Invalid credentials' || 
          error.message.includes('User not found') || 
          error.message.includes('Incorrect password') ||
          error.message.includes('Invalid password')) {
        return res.status(401).json({
          status: 'error',
          message: 'Invalid credentials'
        });
      }
      
      if (error.message === 'User account is inactive' || 
          error.message.includes('inactive')) {
        return res.status(401).json({
          status: 'error',
          message: 'User account is inactive'
        });
      }
      
      return res.status(500).json({ 
        status: 'error', 
        message: 'Authentication failed' 
      });
    }
  }
  
  /**
   * Handle token refresh
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Access token data (only if status is 'success'):
   *     - token {string} - New JWT access token
   *     - expires_at {string} - Token expiration timestamp
   *   - message {string} - Error message (only if status is 'error')
   */
  async handleRefreshToken(req, res) {
    try {
      const oldRefreshTokenString = req.cookies.refreshToken;
      
      if (!oldRefreshTokenString) {
        logger.warn('Refresh token attempt without token cookie', { ip: req.ip });
        return res.status(401).json({
          status: 'error',
          message: 'Invalid or expired refresh token'
        });
      }
      
      // Attempt to refresh tokens
      const refreshResult = await authService.refreshAuthToken(oldRefreshTokenString);
      const { newAccessToken, newRefreshToken, user } = refreshResult;
      
      // Set new refresh token as HttpOnly cookie
      const cookieOptions = {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'Strict',
        maxAge: authConfig.refreshToken.cookieMaxAge,
        path: '/'
      };
      
      res.cookie('refreshToken', newRefreshToken.tokenForCookie, cookieOptions);
      
      logger.info('Token refreshed successfully', { userId: user.id, ip: req.ip });
      
      return res.status(200).json({
        status: 'success',
        data: {
          token: newAccessToken.token,
          expires_at: newAccessToken.expires_at
        }
      });
    } catch (error) {
      logger.warn('Failed token refresh attempt', {
        error: error.message,
        ip: req.ip
      });
      
      // Clear invalid refresh token
      res.clearCookie('refreshToken', {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'Strict',
        path: '/'
      });
      
      // More specific error handling
      if (error.message === 'Invalid or expired refresh token' || 
          error.message.includes('not found') || 
          error.message.includes('expired')) {
        return res.status(401).json({
          status: 'error',
          message: 'Invalid or expired refresh token'
        });
      }
      
      if (error.message === 'Refresh token reuse detected') {
        return res.status(403).json({
          status: 'error',
          message: 'Refresh token reuse detected'
        });
      }
      
      return res.status(500).json({
        status: 'error',
        message: 'Failed to refresh token'
      });
    }
  }
  
  /**
   * Handle user logout
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
   */
  async handleLogout(req, res) {
    try {
      const refreshTokenString = req.cookies.refreshToken;
      
      if (refreshTokenString) {
        await authService.revokeRefreshToken(refreshTokenString);
        logger.info('User logged out and refresh token revoked', { userId: req.user?.id, ip: req.ip });
      } else {
        logger.info('User logged out (no refresh token cookie found)', { userId: req.user?.id, ip: req.ip });
      }
      
      // Clear the refresh token cookie
      res.clearCookie('refreshToken', { 
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'Strict',
        path: '/'
      });
      
      return res.status(200).json({
        status: 'success',
        message: 'Logged out successfully'
      });
    } catch (error) {
      logger.error('Error during logout', {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
        ip: req.ip
      });
      
      return res.status(500).json({
        status: 'error',
        message: 'An error occurred during logout'
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
   *     - created_at {string} - When the user was created (ISO-8601 format)
   *     - updated_at {string} - When the user was last updated (ISO-8601 format)
   *   - message {string} - Error message (only if status is 'error')
   */
  async handleGetMe(req, res) {
    try {
      // Validate that req.user exists (should be set by authentication middleware)
      if (!req.user || !req.user.id) {
        logger.warn('Attempt to get /me without authenticated user', { ip: req.ip });
        return res.status(401).json({
            status: 'error',
            message: 'Unauthorized'
        });
      }
      
      logger.debug(`Fetching user data for ID: ${req.user.id}`, { requestedBy: req.user.id });
      const user = await authService.getUserById(req.user.id);
      
      if (!user) {
        // This shouldn't happen if token is valid and user exists
        logger.error(`User not found for ID ${req.user.id} despite valid token`, { requestedBy: req.user.id });
        return res.status(404).json({
          status: 'error',
          message: 'User not found'
        });
      }
      
      // Format dates to ensure ISO-8601 format
      return res.status(200).json({
        status: 'success',
        data: {
          id: user.id,
          username: user.username,
          role: user.role,
          is_active: user.is_active,
          created_at: user.created_at instanceof Date ? user.created_at.toISOString() : user.created_at,
          updated_at: user.updated_at instanceof Date ? user.updated_at.toISOString() : user.updated_at
        }
      });
    } catch (error) {
      logger.error(`Failed to get user data for ID ${req.user?.id}:`, {
        error: error.message,
        stack: error.stack,
        requestedBy: req.user?.id
      });
      
      return res.status(500).json({
        status: 'error',
        message: 'Failed to retrieve user information'
      });
    }
  }
}

module.exports = new AuthController();