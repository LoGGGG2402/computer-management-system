const db = require('../database/models');
const { Op } = db.Sequelize;

const Computer = db.Computer;
const Room = db.Room;
const UserRoomAssignment = db.UserRoomAssignment;

/**
 * Service class for computer management operations
 */
class ComputerService {
  /**
   * Get all computers with pagination and filters
   * @param {number} page - Page number
   * @param {number} limit - Number of items per page
   * @param {Object} filters - Filter parameters
   * @param {Object} user - Current user object
   * @returns {Object} - Paginated computers list
   */
  async getAllComputers(page = 1, limit = 10, filters = {}, user) {
    try {
      const offset = (page - 1) * limit;
      
      // Build where clause from filters
      let whereClause = {};
      
      if (filters.name) {
        whereClause.name = { [Op.iLike]: `%${filters.name}%` };
      }
      
      if (filters.roomId) {
        whereClause.room_id = filters.roomId;
      }
      
      // Add status filter if provided
      if (filters.status && ['online', 'offline'].includes(filters.status)) {
        whereClause.status = filters.status;
      }
      
      // Add has_errors filter if provided
      if (filters.has_errors === 'true' || filters.has_errors === true) {
        whereClause.has_errors = true;
      }
      
      const { count, rows } = await Computer.findAndCountAll({
        where: whereClause,
        include: [
          {
            model: Room,
            as: 'room',
            attributes: ['id', 'name']
          }
        ],
        limit,
        offset,
        order: [['id', 'ASC']]
      });
      
      // Add has_active_errors field by checking if there are any active errors
      const computersWithErrorStatus = rows.map(computer => {
        const computerData = computer.get({ plain: true });
        // Check if errors array contains any active errors
        let hasActiveErrors = false;
        try {
          const errors = JSON.parse(computer.errors);
          hasActiveErrors = errors.some(error => error.status === 'active');
        } catch (e) {
          // In case of parsing error, default to false
          hasActiveErrors = false;
        }
        return {
          ...computerData,
          has_active_errors: hasActiveErrors
        };
      });
      
      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        computers: computersWithErrorStatus
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get computer by ID
   * @param {number} id - Computer ID
   * @returns {Object} - Computer data
   */
  async getComputerById(id) {
    try {
      const computer = await Computer.findByPk(id, {
        include: [
          {
            model: Room,
            as: 'room',
            attributes: ['id', 'name', 'description']
          }
        ]
      });
      
      if (!computer) {
        throw new Error('Computer not found');
      }
      
      return computer;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Update a computer
   * @param {number} id - Computer ID
   * @param {Object} computerData - Computer data to update
   * @returns {Object} - Updated computer
   */
  async updateComputer(id, computerData) {
    try {
      const computer = await Computer.findByPk(id);
      
      if (!computer) {
        throw new Error('Computer not found');
      }
      
      // If updating room, validate that the room exists
      if (computerData.room_id) {
        const room = await Room.findByPk(computerData.room_id);
        if (!room) {
          throw new Error('Room not found');
        }
      }
      
      // Prepare update data
      const updateData = {};
      
      if (computerData.name !== undefined) updateData.name = computerData.name;
      if (computerData.room_id !== undefined) updateData.room_id = computerData.room_id;
      if (computerData.pos_x !== undefined) updateData.pos_x = computerData.pos_x;
      if (computerData.pos_y !== undefined) updateData.pos_y = computerData.pos_y;
      if (computerData.ip_address !== undefined) updateData.ip_address = computerData.ip_address;
      
      // Update computer
      await computer.update(updateData);
      
      // Fetch updated computer with room
      const updatedComputer = await Computer.findByPk(id, {
        include: [
          {
            model: Room,
            as: 'room',
            attributes: ['id', 'name']
          }
        ]
      });
      
      return updatedComputer;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Delete a computer
   * @param {number} id - Computer ID
   * @returns {boolean} - Success status
   */
  async deleteComputer(id) {
    try {
      const computer = await Computer.findByPk(id);
      
      if (!computer) {
        throw new Error('Computer not found');
      }
      
      // Delete computer
      await computer.destroy();
      
      return true;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Check if user has access to computer's room
   * @param {number} computerId - Computer ID
   * @param {number} userId - User ID
   * @param {string} userRole - User role
   * @returns {boolean} - Whether user has access
   */
  async userHasAccessToComputer(computerId, userId, userRole) {
    try {
      // Admins have access to all computers
      if (userRole === 'admin') {
        return true;
      }
      
      // Get computer with room
      const computer = await Computer.findByPk(computerId);
      
      if (!computer) {
        throw new Error('Computer not found');
      }
      
      // Check if user has assignment to the room
      const assignment = await UserRoomAssignment.findOne({
        where: {
          user_id: userId,
          room_id: computer.room_id
        }
      });
      
      return !!assignment;
    } catch (error) {
      throw error;
    }
  }
}

module.exports = new ComputerService();