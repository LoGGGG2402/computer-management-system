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
   * @param {number} layout.columns - Number of columns in the room grid
   * @param {number} layout.rows - Number of rows in the room grid
   * @returns {boolean} - Validation result (true if layout is valid)
   */
  validateLayout(layout) {
    try {
      if (!layout) return false;

      if (
        !layout.columns ||
        !layout.rows
      ) {
        return false;
      }

      if (
        typeof layout.columns !== "number" ||
        typeof layout.rows !== "number"
      ) {
        return false;
      }

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
   * @param {number} page - Page number (starts from 1)
   * @param {number} limit - Number of items per page
   * @param {string} name - Filter by room name (case-insensitive partial match)
   * @param {number} assigned_user_id - Filter by assigned user ID
   * @param {Object} user - Current user object
   * @param {number} user.id - User ID
   * @param {string} user.role - User role ('admin' or 'user')
   * @returns {Object} - Paginated rooms list with the following properties:
   *   - total {number} - Total number of rooms matching the criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - rooms {Array<Object>} - Array of room objects, each containing:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration with columns and rows
   *     - created_at {Date} - When the room was created
   *     - updated_at {Date} - When the room was last updated
   */
  async getAllRooms(page = 1, limit = 10, name = "", assigned_user_id = null, user) {
    try {
      const offset = (page - 1) * limit;

      let whereClause = {};
      if (name) {
        whereClause.name = { [Op.iLike]: `%${name}%` };
      }

      if (user.role !== "admin") {
        const userAssignments = await UserRoomAssignment.findAll({
          where: { user_id: user.id },
          attributes: ["room_id"],
        });

        const roomIds = userAssignments.map((assignment) => assignment.room_id);

        whereClause.id = { [Op.in]: roomIds };
      }

      let include = [];
      
      if (assigned_user_id) {
        include.push({
          model: User,
          as: 'assignedUsers',
          attributes: [], 
          through: { attributes: [] }, 
          where: { id: assigned_user_id }
        });
      }

      const { count, rows } = await Room.findAndCountAll({
        where: whereClause,
        include,
        distinct: true, 
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
   * @returns {Object} - Room data object with the following properties:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration with columns and rows
   *   - created_at {Date} - When the room was created
   *   - updated_at {Date} - When the room was last updated
   *   - computers {Array<Object>} - Array of computers in this room, each containing:
   *     - id {number} - Computer ID
   *     - name {string} - Computer name
   *     - status {string} - Status ('online' or 'offline')
   *     - pos_x {number} - X position in room grid
   *     - pos_y {number} - Y position in room grid
   *     - room_id {number} - ID of the room this computer belongs to
   *     - has_active_errors {boolean} - Whether the computer has active errors
   *     - last_update {Date} - When the computer was last updated
   *     - os_info {Object} - Operating system information
   *     - cpu_info {Object} - CPU information
   *     - gpu_info {Object} - GPU information
   *     - total_ram {number} - Total RAM in GB
   *     - total_disk_space {number} - Total disk space in GB
   * @throws {Error} - If room is not found
   */
  async getRoomById(id) {
    try {
      const room = await Room.findByPk(id, {
        include: [
          {
            model: Computer,
            as: "computers",
            attributes: {
              exclude: [
                "unique_agent_id",
                "agent_token_hash",
                "error",
              ],
            },
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
   * @param {string} roomData.name - Room name
   * @param {string} [roomData.description=''] - Room description
   * @param {Object} [roomData.layout={}] - Room layout configuration
   * @param {number} [roomData.layout.columns] - Number of columns in the room grid
   * @param {number} [roomData.layout.rows] - Number of rows in the room grid
   * @returns {Object} - Created room data with the following properties:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration
   *   - created_at {Date} - When the room was created
   *   - updated_at {Date} - When the room was last updated
   * @throws {Error} - If room layout format is invalid
   */
  async createRoom(roomData) {
    try {
      if (roomData.layout && !this.validateLayout(roomData.layout)) {
        throw new Error("Invalid room layout format");
      }

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
   * @param {number} id - Room ID to update
   * @param {Object} roomData - Room data to update
   * @param {string} [roomData.name] - New room name
   * @param {string} [roomData.description] - New room description
   * @param {Object} [roomData.layout] - New room layout configuration
   * @param {number} [roomData.layout.columns] - Number of columns in the room grid
   * @param {number} [roomData.layout.rows] - Number of rows in the room grid
   * @returns {Object} - Updated room data with the following properties:
   *   - id {number} - Room ID
   *   - name {string} - Room name
   *   - description {string} - Room description
   *   - layout {Object} - Room layout configuration
   *   - created_at {Date} - When the room was created
   *   - updated_at {Date} - When the room was last updated
   * @throws {Error} - If room is not found or layout format is invalid
   */
  async updateRoom(id, roomData) {
    try {
      const room = await Room.findByPk(id);

      if (!room) {
        throw new Error("Room not found");
      }

      if (roomData.layout && !this.validateLayout(roomData.layout)) {
        throw new Error("Invalid room layout format");
      }

      const updateData = {};

      if (roomData.name !== undefined) updateData.name = roomData.name;
      if (roomData.description !== undefined)
        updateData.description = roomData.description;
      if (roomData.layout !== undefined) updateData.layout = roomData.layout;

      await room.update(updateData);

      const updatedRoom = await Room.findByPk(id);

      return updatedRoom;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Delete a room
   * @param {number} id - Room ID to delete
   * @returns {boolean} - Success status (true if room was successfully deleted)
   * @throws {Error} - If room is not found
   */
  async deleteRoom(id) {
    try {
      const room = await Room.findByPk(id);

      if (!room) {
        throw new Error("Room not found");
      }

      await room.destroy();

      return true;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Assign users to a room
   * @param {number} roomId - Room ID to assign users to
   * @param {number[]} userIds - Array of user IDs to assign
   * @returns {number} - Number of assignments created
   * @throws {Error} - If room is not found or one or more users are not found
   */
  async assignUsersToRoom(roomId, userIds) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

      const users = await User.findAll({
        where: { id: { [Op.in]: userIds } },
      });

      if (users.length !== userIds.length) {
        throw new Error("One or more users not found");
      }

      const assignments = userIds.map((userId) => ({
        user_id: userId,
        room_id: roomId,
      }));

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
   * @param {number} roomId - Room ID to remove users from
   * @param {number[]} userIds - Array of user IDs to unassign
   * @returns {number} - Number of assignments removed
   * @throws {Error} - If room is not found
   */
  async unassignUsersFromRoom(roomId, userIds) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

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
   * @param {number} roomId - Room ID to get assigned users for
   * @returns {Array<Object>} - List of assigned users, each containing:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role (admin/user)
   *   - is_active {boolean} - Whether the user is active
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} - If room is not found
   */
  async getUsersInRoom(roomId) {
    try {
      const room = await Room.findByPk(roomId);

      if (!room) {
        throw new Error("Room not found");
      }

      const users = await User.findAll({
        include: [
          {
            model: Room,
            as: "assignedRooms",
            where: { id: roomId },
            through: { attributes: [] },
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
   * @param {number} posX - X position in the room grid
   * @param {number} posY - Y position in the room grid
   * @returns {Object} - Result with the following properties:
   *   - valid {boolean} - Whether the position is available
   *   - message {string} - Description message about the position status
   *   - room {Object} - Room data if found, null otherwise, containing:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   *     - description {string} - Room description
   *     - layout {Object} - Room layout configuration with columns and rows
   *     - computers {Array<Object>} - Array of computers in this room
   * @throws {Error} - If there's an error checking the position
   */
  async isPositionAvailable(roomName, posX, posY) {
    try {
      const room = await Room.findOne({
        where: { name: roomName },
        include: [{
          model: Computer,
          as: "computers",
          attributes: ['id', 'pos_x', 'pos_y']
        }]
      });

      if (!room) {
        return { 
          valid: false, 
          message: `Phòng "${roomName}" không tồn tại`, 
          room: null 
        };
      }

      if (!room.layout || !room.layout.columns || !room.layout.rows) {
        return { 
          valid: false, 
          message: "Phòng không có cấu hình layout hợp lệ", 
          room 
        };
      }

      if (posX < 0 || posX >= room.layout.columns || posY < 0 || posY >= room.layout.rows) {
        return { 
          valid: false, 
          message: `Vị trí (${posX}, ${posY}) nằm ngoài kích thước phòng (${room.layout.columns}x${room.layout.rows})`, 
          room 
        };
      }

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

      return { 
        valid: true, 
        message: "Vị trí hợp lệ và khả dụng", 
        room 
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get room ID by room name
   * @param {string} roomName - Room name to look up
   * @returns {number|null} - Room ID if found, null otherwise
   * @throws {Error} - If there's an error fetching the room
   */
  async getRoomIdByName(roomName) {
    try {
      const room = await Room.findOne({ where: { name: roomName } });
      return room ? room.id : null;
    } catch (error) {
      throw error;
    }
  }
}

module.exports = new RoomService();
