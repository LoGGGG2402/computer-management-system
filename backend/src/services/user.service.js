const db = require('../database/models');

const User = db.User;

/**
 * Service class for user management operations
 */
class UserService {
  /**
   * Get all users with pagination
   * @param {number} page - Page number
   * @param {number} limit - Number of items per page
   * @param {string} search - Search term for username
   * @returns {Object} - Paginated users list
   */
  async getAllUsers(page = 1, limit = 10, search = '') {
    try {
      const offset = (page - 1) * limit;
      
      const whereClause = search 
        ? { username: { [db.Sequelize.Op.iLike]: `%${search}%` } }
        : {};
      
      const { count, rows } = await User.findAndCountAll({
        where: whereClause,
        limit,
        offset,
        order: [['id', 'ASC']],
        attributes: { exclude: ['password_hash'] }
      });
      
      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        users: rows
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get user by ID
   * @param {number} id - User ID
   * @returns {Object} - User data
   */
  async getUserById(id) {
    try {
      const user = await User.findByPk(id, {
        attributes: { exclude: ['password_hash'] }
      });
      
      if (!user) {
        throw new Error('User not found');
      }
      
      return user;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Create a new user
   * @param {Object} userData - User data
   * @returns {Object} - Created user
   */
  async createUser(userData) {
    try {
      // Check if username already exists
      const existingUser = await User.findOne({
        where: { username: userData.username }
      });
      
      if (existingUser) {
        throw new Error('Username already exists');
      }
      
      // Create user
      const user = await User.create({
        username: userData.username,
        password_hash: userData.password,
        role: userData.role || 'user',
        is_active: userData.is_active !== undefined ? userData.is_active : true
      });
      
      // Return user without password
      const { password_hash, ...userWithoutPassword } = user.toJSON();
      return userWithoutPassword;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Update a user
   * @param {number} id - User ID
   * @param {Object} userData - User data to update
   * @returns {Object} - Updated user
   */
  async updateUser(id, userData) {
    try {
      const user = await User.findByPk(id);
      
      if (!user) {
        throw new Error('User not found');
      }
      
      // If updating username, check if it already exists
      if (userData.username && userData.username !== user.username) {
        const existingUser = await User.findOne({
          where: { username: userData.username }
        });
        
        if (existingUser) {
          throw new Error('Username already exists');
        }
      }
      
      // Prepare update data
      const updateData = {};
      
      if (userData.username) updateData.username = userData.username;
      if (userData.password) updateData.password_hash = userData.password;
      if (userData.role) updateData.role = userData.role;
      if (userData.is_active !== undefined) updateData.is_active = userData.is_active;
      
      // Update user
      await user.update(updateData);
      
      // Fetch updated user
      const updatedUser = await User.findByPk(id, {
        attributes: { exclude: ['password_hash'] }
      });
      
      return updatedUser;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Delete a user (set inactive)
   * @param {number} id - User ID
   * @returns {boolean} - Success status
   */
  async deleteUser(id) {
    try {
      const user = await User.findByPk(id);
      
      if (!user) {
        throw new Error('User not found');
      }
      
      // Instead of deleting, set user as inactive
      await user.update({ is_active: false });
      
      return true;
    } catch (error) {
      throw error;
    }
  }
}

module.exports = new UserService();