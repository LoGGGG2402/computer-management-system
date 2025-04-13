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
   * @param {string} role - Filter by role (admin/user)
   * @param {boolean} is_active - Filter by active status
   * @returns {Object} - Paginated users list
   */
  async getAllUsers(page = 1, limit = 10, search = '', role = null, is_active = null) {
    try {
      const offset = (page - 1) * limit;
      
      // Build where clause
      const whereClause = {};
      
      // Add username search if provided
      if (search) {
        whereClause.username = { [db.Sequelize.Op.iLike]: `%${search}%` };
      }
      
      // Add role filter if provided
      if (role && ['admin', 'user'].includes(role)) {
        whereClause.role = role;
      }
      
      // Add is_active filter if provided
      if (is_active !== null) {
        whereClause.is_active = is_active === 'true' || is_active === true;
      }
      
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

  /**
   * Reactivate an inactive user
   * @param {number} id - User ID to reactivate
   * @returns {Promise<Object>} The reactivated user
   */
  async reactivateUser(id) {
    const user = await User.findByPk(id);
    
    if (!user) {
      throw new Error('User not found');
    }
    
    if (user.is_active) {
      throw new Error('User is already active');
    }
    
    await user.update({ is_active: true });
    
    // Return user without sensitive information
    const { password, ...userData } = user.toJSON();
    return userData;
  }
}

module.exports = new UserService();