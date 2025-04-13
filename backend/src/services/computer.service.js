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
   * @returns {Promise<import("sequelize").Model|null>} Computer object with related room data if found, null otherwise
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
   * @param {import("sequelize").Model|null} computer - Existing computer record to update
   * @returns {Promise<{plainToken: string, computer: import("sequelize").Model}>} Plain text token for agent to store and computer object
   */
  async generateAndAssignAgentToken(
    agentId,
    positionInfo = null,
    computer = null
  ) {
    // Generate a random token
    const plainToken = crypto.randomBytes(32).toString("hex");

    // Hash the token for storage
    const saltRounds = 10;
    const tokenHash = await bcrypt.hash(plainToken, saltRounds);

    // Prepare the update data
    const updateData = {
      agent_token_hash: tokenHash,
      last_update: new Date(),
    };

    if (computer) {
      await computer.update(updateData);
    } else {
      // Find or create the computer record
      computer = await Computer.create({
        unique_agent_id: agentId,
        agent_token_hash: tokenHash,
        name: `Computer-${agentId.substring(0, 8)}`,
        status: "offline",
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
      // Find the computer by agent ID
      const computer = await this.findComputerByAgentId(agentId);

      // If computer not found or no token hash stored, authentication fails
      if (!computer || !computer.agent_token_hash) {
        return null;
      }

      // Compare the provided token with the stored hash
      const isValid = await bcrypt.compare(token, computer.agent_token_hash);

      if (isValid) {
        // If token is valid, update the last_update timestamp
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
   * @param {number} page - The page number for pagination
   * @param {number} limit - Maximum number of items per page
   * @param {Object} filters - Filter criteria for the computer list
   * @param {string|undefined} filters.name - Filter by computer name
   * @param {number|undefined} filters.roomId - Filter by room ID
   * @param {string|undefined} filters.status - Filter by status (online/offline)
   * @param {boolean|string|undefined} filters.has_errors - Filter to show only computers with errors
   * @param {Object} user - Current user object with access permissions
   * @returns {Promise<{total: number, currentPage: number, totalPages: number, computers: Array<Object>}>} Paginated list of computers
   */
  async getAllComputers(page = 1, limit = 10, filters = {}, user) {
    try {
      const offset = (page - 1) * limit;

      // Build the where clause based on filters
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

      // Get computers with count but exclude sensitive fields
      const { count, rows } = await Computer.findAndCountAll({
        attributes: { exclude: ['agent_token_hash', 'unique_agent_id'] },
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
   * @returns {Promise<import("sequelize").Model>} Computer object with room data
   * @throws {Error} If computer is not found
   */
  async getComputerById(id) {
    const computer = await Computer.findByPk(id, {
      attributes: { exclude: ['agent_token_hash', 'unique_agent_id'] },
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
   * @returns {Promise<import("sequelize").Model>} The updated computer object
   * @throws {Error} If computer is not found
   */
  async updateComputer(id, data) {
    const computer = await Computer.findByPk(id);

    if (!computer) {
      throw new Error("Computer not found");
    }

    await computer.update(data);

    // Return without sensitive fields
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
      // First get the computer and its room
      const computer = await Computer.findByPk(computerId);

      if (!computer) return false;

      // Check if the user has access to the computer's room
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
   * @returns {Promise<Array<Object>>} Array of error objects associated with the computer
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
   * @returns {Promise<{error: Object, computerId: number}>} The created error object and computer ID
   * @throws {Error} If computer is not found
   */
  async reportComputerError(id, errorData) {
    const computer = await db.Computer.findByPk(id);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    // Generate unique ID for the error
    const errorId = Date.now();
    errorData.id = errorId;
    
    // Get existing errors and add the new one
    const errors = Array.isArray(computer.errors) ? [...computer.errors] : [];
    errors.push(errorData);
    
    // Update the computer with the new error and set have_active_errors flag
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
   * @returns {Promise<{error: Object, computerId: number}>} The updated error object and computer ID
   * @throws {Error} If computer or error is not found
   */
  async resolveComputerError(computerId, errorId, resolutionNotes) {
    const computer = await db.Computer.findByPk(computerId);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    // Get existing errors
    let errors = Array.isArray(computer.errors) ? [...computer.errors] : [];
    
    // Find the error by ID
    const errorIndex = errors.findIndex(err => err.id === errorId);
    
    if (errorIndex === -1) {
      throw new Error('Error not found for this computer');
    }
    
    // Update the error as resolved
    errors[errorIndex] = {
      ...errors[errorIndex],
      resolved: true,
      resolved_at: new Date(),
      resolution_notes: resolutionNotes || 'Marked as resolved'
    };
    
    // Check if there are any unresolved errors left
    const hasUnresolvedErrors = errors.some(err => !err.resolved);
    
    // Update the computer with the modified errors
    await computer.update({
      errors: errors,
      have_active_errors: hasUnresolvedErrors
    });
    
    return { error: errors[errorIndex], computerId };
  }
}

module.exports = new ComputerService();
