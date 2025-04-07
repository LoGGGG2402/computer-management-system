const db = require('../database/models');

const UserRoomAssignment = db.UserRoomAssignment;

/**
 * Middleware for checking if user has access to a specific room
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const hasRoomAccess = async (req, res, next) => {
  try {
    // req.user is set by the verifyToken middleware
    const userId = req.user.id;
    const roomId = req.params.id || req.body.roomId;

    // Admin has access to all rooms
    if (req.user.role === 'admin') {
      next();
      return;
    }

    // Check if user has assignment to the room
    const assignment = await UserRoomAssignment.findOne({
      where: {
        user_id: userId,
        room_id: roomId
      }
    });

    if (!assignment) {
      return res.status(403).json({
        status: 'error',
        message: 'You do not have access to this room'
      });
    }

    next();
  } catch (error) {
    return res.status(500).json({
      status: 'error',
      message: 'Unable to validate room access'
    });
  }
};

module.exports = {
  hasRoomAccess
};