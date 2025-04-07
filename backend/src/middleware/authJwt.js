const jwt = require('jsonwebtoken');
const authConfig = require('../config/auth.config');
const db = require('../database/models');

const User = db.User;

/**
 * Middleware for verifying JWT token
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const verifyToken = async (req, res, next) => {
  try {
    const token = req.headers['x-access-token'] || req.headers['authorization']?.split(' ')[1];

    if (!token) {
      return res.status(403).json({
        status: 'error',
        message: 'No token provided'
      });
    }

    // Verify token
    const decoded = jwt.verify(token, authConfig.secret);
    req.user = decoded;
    
    // Check if user still exists and is active
    const user = await User.findByPk(decoded.id);
    if (!user || !user.is_active) {
      return res.status(401).json({
        status: 'error',
        message: 'User not found or inactive'
      });
    }

    next();
  } catch (error) {
    return res.status(401).json({
      status: 'error',
      message: 'Unauthorized'
    });
  }
};

module.exports = {
  verifyToken
};