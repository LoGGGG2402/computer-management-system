const db = require('../database/models');
const Computer = db.Computer;
const UserRoomAssignment = db.UserRoomAssignment;

/**
 * Middleware for checking if user has access to a specific computer
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const hasComputerAccess = async (req, res, next) => {
  try {
    // req.user is set by the verifyToken middleware
    const userId = req.user.id;
    const computerId = req.params.id;

    // Admin has access to all computers
    if (req.user.role === 'admin') {
      next();
      return;
    }

    // Get computer with room
    const computer = await Computer.findByPk(computerId);
    
    if (!computer) {
      return res.status(404).json({
        status: 'error',
        message: 'Computer not found'
      });
    }

    // Check if user has assignment to the room
    const assignment = await UserRoomAssignment.findOne({
      where: {
        user_id: userId,
        room_id: computer.room_id
      }
    });

    if (!assignment) {
      return res.status(403).json({
        status: 'error',
        message: 'You do not have access to this computer'
      });
    }

    next();
  } catch (error) {
    return res.status(500).json({
      status: 'error',
      message: 'Unable to validate computer access'
    });
  }
};

module.exports = {
  hasComputerAccess
};