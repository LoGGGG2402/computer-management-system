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
}

module.exports = new RoomService();
