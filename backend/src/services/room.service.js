const db = require("../database/models");
const { Sequelize, Op } = db.Sequelize;

const Room = db.Room;
const User = db.User;
const Computer = db.Computer;
const UserRoomAssignment = db.UserRoomAssignment;

/**
 * Service class for room management operations
 */
class RoomService {
  /**
   * Validate room layout structure
   * @param {Object} layout - Room layout configuration
   * @returns {boolean} - Validation result
   */
  validateLayout(layout) {
    try {
      if (!layout) return false;

      // Check required properties
      if (
        !layout.columns ||
        !layout.rows
      ) {
        return false;
      }

      // Validate data types
      if (
        typeof layout.columns !== "number" ||
        typeof layout.rows !== "number"
      ) {
        return false;
      }

      // Validate values
      if (
        layout.columns <= 0 ||
        layout.rows <= 0
      ) {
        return false;
      }

      return true;
    } catch (error) {
      return false;
    }
  }

  /**
   * Get all rooms with pagination and filters
   * @param {number} page - Page number
   * @param {number} limit - Number of items per page
   * @param {string} name - Filter by room name
   * @param {number} assigned_user_id - Filter by assigned user ID
   * @param {Object} user - Current user object
   * @returns {Object} - Paginated rooms list
   */
  async getAllRooms(page = 1, limit = 10, name = "", assigned_user_id = null, user) {
    try {
      const offset = (page - 1) * limit;

      let whereClause = {};
      if (name) {
        whereClause.name = { [Op.iLike]: `%${name}%` };
      }

      // If not admin, only get rooms that user has access to
      if (user.role !== "admin") {
        // Find all room IDs user has access to
        const userAssignments = await UserRoomAssignment.findAll({
          where: { user_id: user.id },
          attributes: ["room_id"],
        });

        const roomIds = userAssignments.map((assignment) => assignment.room_id);

        // Add room IDs to where clause
        whereClause.id = { [Op.in]: roomIds };
      }

      // Define include criteria for query
      let include = [];
      
      // If filtering by assigned user, modify the query to include that filter
      if (assigned_user_id) {
        include.push({
          model: User,
          as: 'assignedUsers',
          attributes: [], // Don't include user data in the result
          through: { attributes: [] }, // Don't include junction table
          where: { id: assigned_user_id }
        });
      }

      const { count, rows } = await Room.findAndCountAll({
        where: whereClause,
        include,
        distinct: true, // This is important when using includes to get accurate count
        limit,
        offset,
        order: [["id", "ASC"]],
      });

      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        rooms: rows,
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get room by ID with computers
   * @param {number} id - Room ID
   * @returns {Object} - Room data with computers
   */
  async getRoomById(id) {
    try {
      const room = await Room.findByPk(id, {
        include: [
          {
            model: Computer,
            as: "computers",
          },
        ],
      });

      if (!room) {
        throw new Error("Room not found");
      }

      return room;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Create a new room
   * @param {Object} roomData - Room data
   * @returns {Object} - Created room
   */
  async createRoom(roomData) {
    try {
      // Validate layout
      if (roomData.layout && !this.validateLayout(roomData.layout)) {
        throw new Error("Invalid room layout format");
      }

      // Create room
      const room = await Room.create({
        name: roomData.name,
        description: roomData.description || "",
        layout: roomData.layout || {},
      });

      return room;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Update a room
   * @param {number} id - Room ID
   * @param {Object} roomData - Room data to update
   * @returns {Object} - Updated room
   */
  async updateRoom(id, roomData) {
    try {
      const room = await Room.findByPk(id);

      if (!room) {
        throw new Error("Room not found");
      }

      // Validate layout if provided
      if (roomData.layout && !this.validateLayout(roomData.layout)) {
        throw new Error("Invalid room layout format");
      }

      // Prepare update data
      const updateData = {};

      if (roomData.name !== undefined) updateData.name = roomData.name;
      if (roomData.description !== undefined)
        updateData.description = roomData.description;
      if (roomData.layout !== undefined) updateData.layout = roomData.layout;

      // Update room
      await room.update(updateData);

      // Fetch updated room
      const updatedRoom = await Room.findByPk(id);

      return updatedRoom;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Delete a room
   * @param {number} id - Room ID
   * @returns {boolean} - Success status
   */
  async deleteRoom(id) {
    try {
      const room = await Room.findByPk(id);

      if (!room) {
        throw new Error("Room not found");
      }

      // Delete room (will cascade delete related assignments and computers)
      await room.destroy();

      return true;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Assign users to a room
   * @param {number} roomId - Room ID
   * @param {number[]} userIds - Array of user IDs to assign
   * @returns {number} - Number of assignments created
   */
  async assignUsersToRoom(roomId, userIds) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

      // Validate all users exist
      const users = await User.findAll({
        where: { id: { [Op.in]: userIds } },
      });

      if (users.length !== userIds.length) {
        throw new Error("One or more users not found");
      }

      // Prepare assignments data
      const assignments = userIds.map((userId) => ({
        user_id: userId,
        room_id: roomId,
      }));

      // Create assignments (ignore duplicates)
      const result = await UserRoomAssignment.bulkCreate(assignments, {
        ignoreDuplicates: true,
      });

      return result.length;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Remove user assignments from a room
   * @param {number} roomId - Room ID
   * @param {number[]} userIds - Array of user IDs to unassign
   * @returns {number} - Number of assignments removed
   */
  async unassignUsersFromRoom(roomId, userIds) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

      // Delete assignments
      const result = await UserRoomAssignment.destroy({
        where: {
          room_id: roomId,
          user_id: { [Op.in]: userIds },
        },
      });

      return result;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get users assigned to a room
   * @param {number} roomId - Room ID
   * @returns {Array} - List of assigned users
   */
  async getUsersInRoom(roomId) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

      // Get users assigned to the room
      const users = await User.findAll({
        include: [
          {
            model: Room,
            as: "assignedRooms",
            where: { id: roomId },
            through: { attributes: [] }, // Don't include junction table
          },
        ],
        attributes: { exclude: ["password_hash"] },
      });

      return users;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Check if a position in a room is available
   * @param {string} roomName - Room name
   * @param {number} posX - X position
   * @param {number} posY - Y position
   * @returns {Object} - Result {valid: boolean, message: string, room: Object}
   */
  async isPositionAvailable(roomName, posX, posY) {
    try {
      // Find the room by name
      const room = await Room.findOne({
        where: { name: roomName },
        include: [{
          model: Computer,
          as: "computers",
          attributes: ['id', 'pos_x', 'pos_y']
        }]
      });

      // If room doesn't exist
      if (!room) {
        return { 
          valid: false, 
          message: `Phòng "${roomName}" không tồn tại`, 
          room: null 
        };
      }

      // Validate position against room dimensions
      if (!room.layout || !room.layout.columns || !room.layout.rows) {
        return { 
          valid: false, 
          message: "Phòng không có cấu hình layout hợp lệ", 
          room 
        };
      }

      // Check if position is within room bounds
      if (posX < 0 || posX >= room.layout.columns || posY < 0 || posY >= room.layout.rows) {
        return { 
          valid: false, 
          message: `Vị trí (${posX}, ${posY}) nằm ngoài kích thước phòng (${room.layout.columns}x${room.layout.rows})`, 
          room 
        };
      }

      // Check if position is already occupied by another computer
      const isOccupied = room.computers.some(
        comp => comp.pos_x === posX && comp.pos_y === posY
      );

      if (isOccupied) {
        return { 
          valid: false, 
          message: `Vị trí (${posX}, ${posY}) đã được sử dụng bởi một máy tính khác`, 
          room 
        };
      }

      // Position is valid and available
      return { 
        valid: true, 
        message: "Vị trí hợp lệ và khả dụng", 
        room 
      };
    } catch (error) {
      console.error("Error checking position availability:", error);
      throw error;
    }
  }

  /**
   * Check if a room name matches a given room ID
   * @param {string} roomName - The name of the room
   * @param {number} roomId - The room ID to check against
   * @returns {Promise<boolean>} - True if the room name matches the given ID
   */
  async isRoomNameMatchesId(roomName, roomId) {
    try {
      if (!roomName || !roomId) {
        return false;
      }

      // Find the room by its ID
      const room = await Room.findByPk(roomId);
      
      // If room doesn't exist or name doesn't match
      if (!room || room.name !== roomName) {
        return false;
      }
      
      return true;
    } catch (error) {
      console.error("Error checking room name match:", error);
      return false; // Return false on any error
    }
  }
}

module.exports = new RoomService();
