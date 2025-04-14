const bcrypt = require("bcryptjs");
const crypto = require("crypto");
const db = require("../database/models");
const { Op } = require("sequelize");

const Computer = db.Computer;
const Room = db.Room;

/**
 * Service providing functionality for managing computer entities in the system.
 * Handles CRUD operations, status tracking, and agent authentication.
 */
class ComputerService {
  /**
   * Finds a computer in the database by its unique agent identifier.
   * 
   * @param {string} agentId - The unique identifier assigned to the agent
   * @returns {Promise<Object|null>} Computer object with related room data if found, null otherwise
   *   - id {number} - The database identifier of the computer
   *   - name {string} - Human-readable name of the computer
   *   - unique_agent_id {string} - Unique identifier for the agent
   *   - agent_token_hash {string} - Hashed authentication token
   *   - status {string} - Current status ('online' or 'offline')
   *   - has_active_errors {boolean} - Whether the computer has unresolved errors
   *   - last_update {Date} - When the computer was last updated
   *   - room_id {number} - ID of the room where the computer is located
   *   - pos_x {number} - X position in the room grid
   *   - pos_y {number} - Y position in the room grid
   *   - os_info {Object} - Information about the operating system
   *   - cpu_info {Object} - Information about the CPU
   *   - gpu_info {Object} - Information about the GPU
   *   - total_ram {number} - Total RAM in GB
   *   - total_disk_space {number} - Total disk space in GB
   *   - room {Object} - Room data with:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   */
  async findComputerByAgentId(agentId) {
    return Computer.findOne({
      where: { unique_agent_id: agentId },
      include: [
        {
          model: Room,
          as: "room",
          attributes: ["id", "name"],
        },
      ]
    });
  }

  /**
   * Generates a secure token and assigns it to a computer agent.
   * 
   * @param {string} agentId - The unique identifier for the agent
   * @param {Object|null} positionInfo - Information about the physical position of the computer
   * @param {number|null} positionInfo.roomId - ID of the room where the computer is located
   * @param {number|null} positionInfo.posX - X-position within the room grid
   * @param {number|null} positionInfo.posY - Y-position within the room grid
   * @param {Object|null} computer - Existing computer record to update
   * @returns {Promise<Object>} Object containing:
   *   - plainToken {string} - Plain text token for agent to store
   *   - computer {Object} - Updated computer object with the following properties:
   *     - id {number} - The database identifier of the computer
   *     - name {string} - Human-readable name of the computer
   *     - unique_agent_id {string} - Unique identifier for the agent
   *     - agent_token_hash {string} - Hashed authentication token
   *     - status {string} - Current status ('online' or 'offline')
   *     - has_active_errors {boolean} - Whether the computer has unresolved errors
   *     - last_update {Date} - When the computer was last updated
   *     - room_id {number} - ID of the room where the computer is located
   *     - pos_x {number} - X position in the room grid
   *     - pos_y {number} - Y position in the room grid
   */
  async generateAndAssignAgentToken(
    agentId,
    positionInfo = null,
    computer = null
  ) {
    const plainToken = crypto.randomBytes(32).toString("hex");

    const saltRounds = 10;
    const tokenHash = await bcrypt.hash(plainToken, saltRounds);

    const updateData = {
      agent_token_hash: tokenHash,
      last_update: new Date(),
    };

    if (computer) {
      await computer.update(updateData);
    } else {
      computer = await Computer.create({
        unique_agent_id: agentId,
        agent_token_hash: tokenHash,
        name: `Computer-${agentId.substring(0, 8)}`,
        status: "offline",
        errors: [],
        has_active_errors: false,
        last_update: new Date(),
        room_id: positionInfo?.roomId,
        pos_x: positionInfo?.posX,
        pos_y: positionInfo?.posY,
      });
    }

    return {plainToken, computer};
  }

  /**
   * Verifies the authenticity of an agent token for a specific agent.
   * 
   * @param {string} agentId - The unique identifier for the agent
   * @param {string} token - The token to verify against stored hash
   * @returns {Promise<number|null>} The computer ID if token is valid, null otherwise
   */
  async verifyAgentToken(agentId, token) {
    try {
      const computer = await this.findComputerByAgentId(agentId);

      if (!computer || !computer.agent_token_hash) {
        return null;
      }

      const isValid = await bcrypt.compare(token, computer.agent_token_hash);

      if (isValid) {
        await computer.update({ last_update: new Date() });
        return computer.id;
      }

      return null;
    } catch (error) {
      console.error("Error verifying agent token:", error);
      throw error;
    }
  }

  /**
   * Retrieves a paginated and filtered list of computers.
   * 
   * @param {number} page - The page number for pagination (1-based)
   * @param {number} limit - Maximum number of items per page
   * @param {Object} filters - Filter criteria for the computer list
   * @param {string} [filters.name] - Filter by computer name (partial match)
   * @param {number} [filters.roomId] - Filter by room ID
   * @param {string} [filters.status] - Filter by status ('online'/'offline')
   * @param {boolean|string} [filters.has_errors] - Filter to show only computers with errors
   * @param {Object} user - Current user object with access permissions
   * @param {number} user.id - User's ID
   * @param {string} user.role - User's role ('admin' or 'user')
   * @returns {Promise<Object>} Object containing:
   *   - total {number} - Total number of computers matching the criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - computers {Array<Object>} - Array of computer objects:
   *     - id {number} - Computer ID
   *     - name {string} - Computer name
   *     - status {string} - Status ('online' or 'offline')
   *     - has_active_errors {boolean} - Whether computer has errors
   *     - last_update {Date} - When the computer was last updated
   *     - room_id {number} - ID of the room where the computer is located
   *     - pos_x {number} - X position in the room grid
   *     - pos_y {number} - Y position in the room grid
   *     - os_info {Object} - Operating system information
   *     - cpu_info {Object} - CPU information
   *     - gpu_info {Object} - GPU information
   *     - total_ram {number} - Total RAM in GB
   *     - total_disk_space {number} - Total disk space in GB
   *     - room {Object} - Associated room information:
   *       - id {number} - Room ID
   *       - name {string} - Room name
   */
  async getAllComputers(page = 1, limit = 10, filters = {}, user) {
    try {
      const offset = (page - 1) * limit;

      const whereClause = {};

      if (filters.name) {
        whereClause.name = { [Op.iLike]: `%${filters.name}%` };
      }

      if (filters.roomId) {
        whereClause.room_id = filters.roomId;
      }

      if (filters.status && ["online", "offline"].includes(filters.status)) {
        whereClause.status = filters.status;
      }

      if (filters.has_errors === "true") {
        whereClause.has_active_errors = true;
      }

      const { count, rows } = await Computer.findAndCountAll({
        attributes: { exclude: ['agent_token_hash', 'unique_agent_id', 'errors'] },
        where: whereClause,
        include: [
          {
            model: Room,
            as: "room",
            attributes: ["id", "name"],
          },
        ],
        limit,
        offset,
        order: [["id", "ASC"]],
      });
      
      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        computers: rows,
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Retrieves a computer by its unique ID.
   * 
   * @param {number} id - The database identifier of the computer
   * @returns {Promise<Object>} Computer object with the following properties:
   *   - id {number} - Computer ID
   *   - name {string} - Computer name
   *   - status {string} - Status ('online' or 'offline')
   *   - has_active_errors {boolean} - Whether computer has errors
   *   - last_update {Date} - When the computer was last updated
   *   - room_id {number} - ID of the room where the computer is located
   *   - pos_x {number} - X position in the room grid
   *   - pos_y {number} - Y position in the room grid
   *   - os_info {Object} - Operating system information
   *   - cpu_info {Object} - CPU information
   *   - gpu_info {Object} - GPU information
   *   - total_ram {number} - Total RAM in GB
   *   - total_disk_space {number} - Total disk space in GB
   *   - room {Object} - Associated room with:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   * @throws {Error} If computer is not found
   */
  async getComputerById(id) {
    const computer = await Computer.findByPk(id, {
      attributes: { exclude: ['agent_token_hash', 'unique_agent_id', 'errors'] },
      include: [
        {
          model: Room,
          as: "room",
          attributes: ["id", "name"],
        },
      ],
    });

    if (!computer) {
      throw new Error("Computer not found");
    }

    return computer;
  }

  /**
   * Updates a computer record with new data.
   * 
   * @param {number} id - The database identifier of the computer to update
   * @param {Object} data - The data fields to update on the computer record
   * @param {string} [data.name] - New computer name
   * @param {string} [data.status] - New status ('online' or 'offline')
   * @param {boolean} [data.has_active_errors] - New error status
   * @param {number} [data.room_id] - New room ID
   * @param {number} [data.pos_x] - New X position in room grid
   * @param {number} [data.pos_y] - New Y position in room grid
   * @param {Object} [data.os_info] - New operating system information
   * @param {Object} [data.cpu_info] - New CPU information
   * @param {Object} [data.gpu_info] - New GPU information
   * @param {number} [data.total_ram] - New total RAM value in GB
   * @param {number} [data.total_disk_space] - New total disk space value in GB
   * @returns {Promise<Object>} The updated computer object without sensitive fields
   * @throws {Error} If computer is not found
   */
  async updateComputer(id, data) {
    const computer = await Computer.findByPk(id);

    if (!computer) {
      throw new Error("Computer not found");
    }

    await computer.update(data);

    const { agent_token_hash, unique_agent_id, ...safeData } = computer.get({ plain: true });
    return safeData;
  }

  /**
   * Updates the last_update timestamp for a computer.
   * 
   * @param {number} computerId - The database identifier of the computer
   * @returns {Promise<boolean>} True if update was successful
   * @throws {Error} If computer is not found or update fails
   */
  async updateLastSeen(computerId) {
    try {
      const computer = await Computer.findByPk(computerId);

      if (!computer) {
        throw new Error("Computer not found");
      }

      await computer.update({ last_update: new Date() });

      return true;
    } catch (error) {
      console.error("Error updating computer last seen timestamp:", error);
      throw error;
    }
  }

  /**
   * Deletes a computer from the database.
   * 
   * @param {number} id - The database identifier of the computer to delete
   * @returns {Promise<boolean>} True if deletion was successful
   * @throws {Error} If computer is not found or deletion fails
   */
  async deleteComputer(id) {
    const computer = await Computer.findByPk(id);

    if (!computer) {
      throw new Error("Computer not found");
    }

    await computer.destroy();

    return true;
  }

  /**
   * Checks if a user has access to a specific computer via room assignments.
   * 
   * @param {number} userId - The database identifier of the user
   * @param {number} computerId - The database identifier of the computer
   * @returns {Promise<boolean>} True if user has access to the computer, false otherwise
   */
  async checkUserComputerAccess(userId, computerId) {
    try {
      const computer = await Computer.findByPk(computerId);

      if (!computer) return false;

      const userRoomAssignment = await db.UserRoomAssignment.findOne({
        where: {
          user_id: userId,
          room_id: computer.room_id
        }
      });

      return userRoomAssignment !== null;
    } catch (error) {
      console.error(`Error checking user computer access: ${error.message}`);
      return false;
    }
  }

  /**
   * Retrieves all error records for a specific computer.
   * 
   * @param {number} id - The database identifier of the computer
   * @returns {Promise<Array<Object>>} Array of error objects with the following properties:
   *   - id {number} - Error ID
   *   - error_type {string} - Type/category of the error
   *   - error_message {string} - Human-readable error message
   *   - error_details {Object} - Additional details about the error
   *   - reported_at {Date} - When the error was reported
   *   - resolved {boolean} - Whether the error has been resolved
   *   - resolved_at {Date} - When the error was resolved (if applicable)
   *   - resolution_notes {string} - Notes about how the error was resolved
   * @throws {Error} If computer is not found
   */
  async getComputerErrors(id) {
    const computer = await db.Computer.findByPk(id, {
      attributes: ['id', 'name', 'errors', 'have_active_errors']
    });
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    return computer.errors || [];
  }

  /**
   * Records a new error for a computer and updates its error status.
   * 
   * @param {number} id - The database identifier of the computer
   * @param {Object} errorData - Data describing the error
   * @param {string} errorData.error_type - Type/category of the error
   * @param {string} errorData.error_message - Human-readable error message
   * @param {Object} [errorData.error_details={}] - Additional details about the error
   * @returns {Promise<Object>} Object containing:
   *   - error {Object} - The created error object with:
   *     - id {number} - Generated error ID
   *     - error_type {string} - Type/category of the error
   *     - error_message {string} - Human-readable error message
   *     - error_details {Object} - Additional details about the error
   *     - reported_at {Date} - When the error was reported
   *     - resolved {boolean} - Set to false for new errors
   *   - computerId {number} - The computer ID associated with this error
   * @throws {Error} If computer is not found
   */
  async reportComputerError(id, errorData) {
    const computer = await db.Computer.findByPk(id);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    const errorId = Date.now();
    errorData.id = errorId;
    
    const errors = Array.isArray(computer.errors) ? [...computer.errors] : [];
    errors.push(errorData);
    
    await computer.update({
      errors: errors,
      have_active_errors: true
    });
    
    return { error: errorData, computerId: id };
  }

  /**
   * Marks a computer error as resolved and updates the error status.
   * 
   * @param {number} computerId - The database identifier of the computer
   * @param {number} errorId - The identifier of the specific error to resolve
   * @param {string} [resolutionNotes=''] - Notes detailing the resolution
   * @returns {Promise<Object>} Object containing:
   *   - error {Object} - The updated error object with:
   *     - id {number} - Error ID
   *     - error_type {string} - Type/category of the error
   *     - error_message {string} - Human-readable error message
   *     - error_details {Object} - Additional details about the error
   *     - reported_at {Date} - When the error was reported
   *     - resolved {boolean} - Set to true
   *     - resolved_at {Date} - When the error was marked as resolved
   *     - resolution_notes {string} - Notes detailing the resolution
   *   - computerId {number} - The computer ID associated with this error
   * @throws {Error} If computer or error is not found
   */
  async resolveComputerError(computerId, errorId, resolutionNotes) {
    const computer = await db.Computer.findByPk(computerId);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    let errors = Array.isArray(computer.errors) ? [...computer.errors] : [];
    
    const errorIndex = errors.findIndex(err => err.id === errorId);
    
    if (errorIndex === -1) {
      throw new Error('Error not found for this computer');
    }
    
    errors[errorIndex] = {
      ...errors[errorIndex],
      resolved: true,
      resolved_at: new Date(),
      resolution_notes: resolutionNotes || 'Marked as resolved'
    };
    
    const hasUnresolvedErrors = errors.some(err => !err.resolved);
    
    await computer.update({
      errors: errors,
      have_active_errors: hasUnresolvedErrors
    });
    
    return { error: errors[errorIndex], computerId };
  }
}

module.exports = new ComputerService();
