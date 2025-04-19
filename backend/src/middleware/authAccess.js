const db = require('../database/models');
const computerService = require('../services/computer.service');
const logger = require('../utils/logger');

const UserRoomAssignment = db.UserRoomAssignment;

/**
 * Middleware for checking user access based on role, room, or computer.
 * @param {Object} options - Configuration options for the middleware.
 * @param {string} [options.requiredRole] - The role required for access (e.g., 'admin').
 * @param {boolean} [options.checkRoomIdParam] - Whether to check access based on the 'roomId' param.
 * @param {boolean} [options.checkComputerIdParam] - Whether to check access based on the 'computerId' param.
 */
const authAccess = (options = {}) => {
  return async (req, res, next) => {
    try {
      const userId = req.user?.id;
      const userRole = req.user?.role;
      const endpoint = `${req.method} ${req.originalUrl}`;

      if (!userId) {
        // This should ideally be caught by verifyToken first, but added as a safeguard
        logger.warn('Access check failed: User not authenticated', { endpoint });
        return res.status(401).json({ status: 'error', message: 'Authentication required' });
      }

      // 1. Admin Check (if required or if user is admin)
      const isAdmin = userRole === 'admin';
      if (options.requiredRole === 'admin' && !isAdmin) {
        logger.warn(`Access denied: Admin role required for user ${userId}`, { userId, role: userRole, endpoint, ip: req.ip });
        return res.status(403).json({ status: 'error', message: 'Require Admin Role!' });
      }

      // Admins bypass specific resource checks unless explicitly denied elsewhere
      if (isAdmin) {
         logger.debug(`Admin access granted for user ${userId}`, { endpoint });
         return next();
      }

      // 2. Room Access Check
      if (options.checkRoomIdParam) {
        const roomId = parseInt(req.params.roomId);
        if (!roomId) {
          logger.debug('Room access check failed: Missing room ID parameter', { endpoint });
          return res.status(400).json({ status: 'error', message: 'Room ID is required' });
        }

        const hasAccess = await UserRoomAssignment.findOne({ where: { user_id: userId, room_id: roomId } });
        if (!hasAccess) {
          logger.warn(`Access denied to room ${roomId} for user ${userId}`, { userId, roomId, role: userRole, endpoint, ip: req.ip });
          return res.status(403).json({ status: 'error', message: 'You do not have access to this room' });
        }
         logger.debug(`Access granted to room ${roomId} for user ${userId}`, { endpoint });
         // Continue to next check if any, otherwise proceed
      }

      // 3. Computer Access Check
      if (options.checkComputerIdParam) {
        const computerId = parseInt(req.params.computerId);
        if (!computerId) {
          logger.debug('Computer access check failed: Missing computer ID parameter', { endpoint });
          return res.status(400).json({ status: 'error', message: 'Computer ID is required' });
        }

        const hasAccess = await computerService.checkUserComputerAccess(userId, computerId);
        if (!hasAccess) {
          logger.warn(`Access denied to computer ${computerId} for user ${userId}`, { userId, computerId, role: userRole, endpoint, ip: req.ip });
          return res.status(403).json({ status: 'error', message: 'You do not have access to this computer' });
        }
         logger.debug(`Access granted to computer ${computerId} for user ${userId}`, { endpoint });
         // Continue to next check if any, otherwise proceed
      }

      // If no specific checks failed or were required (beyond potential admin check), grant access
      next();

    } catch (error) {
      logger.error('Access check error:', {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
        params: req.params,
        endpoint: `${req.method} ${req.originalUrl}`
      });
      res.status(500).json({ status: 'error', message: 'Internal server error during access check' });
    }
  };
};

module.exports = { authAccess };
