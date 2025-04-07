const jwt = require('jsonwebtoken');
const db = require('../database/models');
const authConfig = require('../config/auth.config');

const User = db.User;

/**
 * Authentication service for user login and token generation
 */
class AuthService {
  /**
   * Authenticates a user and generates a JWT token
   * @param {string} username - The username
   * @param {string} password - The password
   * @returns {Object} - User data and token if authentication is successful
   */
  async login(username, password) {
    try {
      // Find user by username
      const user = await User.findOne({ where: { username, is_active: true } });
      
      if (!user) {
        throw new Error('User not found or inactive');
      }
      
      // Verify password
      const passwordIsValid = await user.validPassword(password);
      
      if (!passwordIsValid) {
        throw new Error('Invalid password');
      }
      
      // Generate JWT token
      const token = jwt.sign(
        { 
          id: user.id,
          username: user.username,
          role: user.role 
        },
        authConfig.secret,
        { expiresIn: authConfig.expiresIn }
      );
      
      return {
        id: user.id,
        username: user.username,
        role: user.role,
        token: token
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Gets user details by id
   * @param {number} userId - The user id
   * @returns {Object} - User data
   */
  async getUserById(userId) {
    try {
      const user = await User.findByPk(userId, {
        attributes: ['id', 'username', 'role', 'is_active', 'created_at', 'updated_at']
      });
      
      if (!user) {
        throw new Error('User not found');
      }
      
      return user;
    } catch (error) {
      throw error;
    }
  }
}

module.exports = new AuthService();