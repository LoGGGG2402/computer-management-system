const bcrypt = require('bcryptjs');
const crypto = require('crypto');
const db = require('../database/models');
const { Op } = require('sequelize');

const Computer = db.Computer;
const Room = db.Room;

/**
 * Service for computer management operations
 */
class ComputerService {
  /**
   * Find a computer by its unique agent ID
   * @param {string} agentId - The unique agent ID
   * @returns {Promise<Object>} The computer object if found, null otherwise
   */
  async findComputerByAgentId(agentId) {
    return Computer.findOne({
      where: { unique_agent_id: agentId }
    });
  }

  /**
   * Generate a new token and assign it to an agent
   * @param {string} agentId - The unique agent ID
   * @returns {Promise<string>} The plain text token for the agent to store
   */
  async generateAndAssignAgentToken(agentId) {
    // Generate a random token
    const plainToken = crypto.randomBytes(32).toString('hex');
    
    // Hash the token for storage
    const saltRounds = 10;
    const tokenHash = await bcrypt.hash(plainToken, saltRounds);
    
    // Find or create the computer record
    let computer = await this.findComputerByAgentId(agentId);
    
    if (computer) {
      // Update the existing computer
      await computer.update({
        agent_token_hash: tokenHash,
        last_seen: new Date()
      });
    } else {
      // Create a new computer record
      computer = await Computer.create({
        unique_agent_id: agentId,
        agent_token_hash: tokenHash,
        name: `Computer-${agentId.substring(0, 8)}`,
        status: 'offline',
        has_active_errors: false,
        last_seen: new Date()
      });
    }
    
    return plainToken;
  }

  /**
   * Verify an agent token
   * @param {string} agentId - The unique agent ID
   * @param {string} token - The token to verify
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
        // If token is valid, update the last_seen timestamp
        await computer.update({ last_seen: new Date() });
        return computer.id;
      }
      
      return null;
    } catch (error) {
      console.error('Error verifying agent token:', error);
      throw error;
    }
  }

  /**
   * Get all computers with pagination and filtering
   * @param {number} page - The page number
   * @param {number} limit - The number of items per page
   * @param {Object} filters - Filtering options
   * @param {Object} user - The user making the request
   * @returns {Promise<Object>} Paginated list of computers
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
      
      if (filters.status && ['online', 'offline'].includes(filters.status)) {
        whereClause.status = filters.status;
      }
      
      if (filters.has_errors === 'true') {
        whereClause.has_active_errors = true;
      }
      
      // Get computers with count
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
      
      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        computers: rows
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get computer by ID
   * @param {number} id - The computer ID
   * @returns {Promise<Object>} The computer object
   */
  async getComputerById(id) {
    const computer = await Computer.findByPk(id, {
      include: [
        {
          model: Room,
          as: 'room',
          attributes: ['id', 'name']
        }
      ]
    });
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    return computer;
  }

  /**
   * Update a computer
   * @param {number} id - The computer ID
   * @param {Object} data - The data to update
   * @returns {Promise<Object>} The updated computer object
   */
  async updateComputer(id, data) {
    const computer = await Computer.findByPk(id);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    await computer.update(data);
    
    return computer;
  }

  /**
   * Delete a computer
   * @param {number} id - The computer ID
   * @returns {Promise<boolean>} True if deleted, throws error otherwise
   */
  async deleteComputer(id) {
    const computer = await Computer.findByPk(id);
    
    if (!computer) {
      throw new Error('Computer not found');
    }
    
    await computer.destroy();
    
    return true;
  }
}

module.exports = new ComputerService();