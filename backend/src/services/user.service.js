const db = require("../database/models");
const authService = require("./auth.service");

const User = db.User;
const RefreshToken = db.RefreshToken;

/**
 * Service class for user management operations
 */
class UserService {
  /**
   * Get all users with pagination
   * @param {number} page - Page number (starts from 1)
   * @param {number} limit - Number of items per page
   * @param {string} search - Search term for username (case-insensitive partial match)
   * @param {string} role - Filter by role (admin/user)
   * @param {boolean} is_active - Filter by active status (true/false)
   * @returns {Object} - Paginated users list with the following properties:
   *   - total {number} - Total number of users matching the criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - users {Array<Object>} - Array of user objects, each containing:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether the user is active
   *     - created_at {Date} - When the user was created
   *     - updated_at {Date} - When the user was last updated
   */
  async getAllUsers(
    page = 1,
    limit = 10,
    search = "",
    role = null,
    is_active = null
  ) {
    try {
      const offset = (page - 1) * limit;

      const whereClause = {};

      if (search) {
        whereClause.username = { [db.Sequelize.Op.iLike]: `%${search}%` };
      }

      if (role && ["admin", "user"].includes(role)) {
        whereClause.role = role;
      }

      if (is_active !== null) {
        whereClause.is_active = is_active === "true" || is_active === true;
      }

      const { count, rows } = await User.findAndCountAll({
        where: whereClause,
        limit,
        offset,
        order: [["id", "ASC"]],
        attributes: { exclude: ["password_hash"] },
      });

      return {
        total: count,
        currentPage: page,
        totalPages: Math.ceil(count / limit),
        users: rows,
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Get user by ID
   * @param {number} id - User ID to retrieve
   * @returns {Object} - User data with the following properties:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role (admin/user)
   *   - is_active {boolean} - Whether the user is active
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} - If user is not found
   */
  async getUserById(id) {
    try {
      const user = await User.findByPk(id, {
        attributes: { exclude: ["password_hash"] },
      });

      if (!user) {
        throw new Error("User not found");
      }

      return user;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Create a new user
   * @param {Object} userData - User data
   * @param {string} userData.username - Username (unique)
   * @param {string} userData.password - Plain text password to be hashed
   * @param {string} [userData.role='user'] - User role (admin/user)
   * @param {boolean} [userData.is_active=true] - Whether the user is active
   * @returns {Object} - Created user data with the following properties:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role (admin/user)
   *   - is_active {boolean} - Whether the user is active
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} - If username already exists
   */
  async createUser(userData) {
    try {
      const existingUser = await User.findOne({
        where: { username: userData.username },
      });

      if (existingUser) {
        throw new Error("Username already exists");
      }

      const user = await User.create({
        username: userData.username,
        password_hash: userData.password,
        role: userData.role || "user",
        is_active: userData.is_active !== undefined ? userData.is_active : true,
      });

      const { password_hash, ...userWithoutPassword } = user.toJSON();
      return userWithoutPassword;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Update a user
   * @param {number} id - User ID to update
   * @param {Object} userData - User data to update
   * @param {string} [userData.username] - New username (must be unique)
   * @param {string} [userData.password] - New password to be hashed
   * @param {string} [userData.role] - New role (admin/user)
   * @param {boolean} [userData.is_active] - New active status
   * @returns {Object} - Updated user data with the following properties:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role (admin/user)
   *   - is_active {boolean} - Whether the user is active
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} - If user is not found or if new username already exists
   */
  async updateUser(id, userData) {
    try {
      const user = await User.findByPk(id);

      if (!user) {
        throw new Error("User not found");
      }

      if (userData.username && userData.username !== user.username) {
        const existingUser = await User.findOne({
          where: { username: userData.username },
        });

        if (existingUser) {
          throw new Error("Username already exists");
        }
      }

      const updateData = {};

      if (userData.username) updateData.username = userData.username;
      if (userData.password) updateData.password_hash = userData.password;
      if (userData.role) updateData.role = userData.role;
      if (userData.is_active !== undefined)
        updateData.is_active = userData.is_active;

      // Check if password is being updated
      const isPasswordChange = !!userData.password;

      await user.update(updateData);

      // If password was changed, invalidate all refresh tokens for security
      if (isPasswordChange) {
        // Invalidate all tokens for this user
        await RefreshToken.destroy({ where: { user_id: id } });
      }

      const updatedUser = await User.findByPk(id, {
        attributes: { exclude: ["password_hash"] },
      });

      return updatedUser;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Delete a user (set inactive)
   * @param {number} id - User ID to delete
   * @returns {boolean} - Success status (true if user was successfully set to inactive)
   * @throws {Error} - If user is not found
   */
  async deleteUser(id) {
    try {
      const user = await User.findByPk(id);

      if (!user) {
        throw new Error("User not found");
      }

      await user.update({ is_active: false });

      return true;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Reactivate an inactive user
   * @param {number} id - User ID to reactivate
   * @returns {Promise<Object>} - The reactivated user data with the following properties:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role (admin/user)
   *   - is_active {boolean} - Will be true after reactivation
   *   - created_at {Date} - When the user was created
   *   - updated_at {Date} - When the user was last updated
   * @throws {Error} - If user is not found or is already active
   */
  async reactivateUser(id) {
    const user = await User.findByPk(id);

    if (!user) {
      throw new Error("User not found");
    }

    if (user.is_active) {
      throw new Error("User is already active");
    }

    await user.update({ is_active: true });

    const { password, ...userData } = user.toJSON();
    return userData;
  }
}

module.exports = new UserService();
