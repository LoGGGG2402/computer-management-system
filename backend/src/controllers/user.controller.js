const userService = require("../services/user.service");
const logger = require("../utils/logger");
const validationUtils = require("../utils/validation");

/**
 * Controller for user management operations
 */
class UserController {
  /**
   * Get all users with pagination
   * @param {Object} req - Express request object
   * @param {Object} req.query - Query parameters
   * @param {number} [req.query.page=1] - Page number for pagination
   * @param {number} [req.query.limit=10] - Number of users per page
   * @param {string} [req.query.username] - Filter by username (partial match)
   * @param {string} [req.query.role] - Filter by role (admin/user)
   * @param {boolean|string} [req.query.is_active] - Filter by active status
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Pagination result with:
   *     - total {number} - Total number of users matching criteria
   *     - currentPage {number} - Current page number
   *     - totalPages {number} - Total number of pages
   *     - users {Array<Object>} - Array of user objects:
   *       - id {number} - User ID
   *       - username {string} - Username
   *       - role {string} - User role (admin/user)
   *       - is_active {boolean} - Whether user is active
   *       - created_at {string} - When user was created (ISO-8601 format)
   *       - updated_at {string} - When user was last updated (ISO-8601 format)
   *   - message {string} - Error message (only if status is 'error')
   */
  async getAllUsers(req, res) {
    try {
      const errors = [];

      if (req.query.page !== undefined) {
        const pageError = validationUtils.validatePageQueryParam(
          req.query.page
        );
        if (pageError) {
          errors.push({ field: "page", message: pageError });
        }
      }

      if (req.query.limit !== undefined) {
        const limitError = validationUtils.validateLimitQueryParam(
          req.query.limit
        );
        if (limitError) {
          errors.push({ field: "limit", message: limitError });
        }
      }

      if (req.query.username) {
        const usernameSearchError = validationUtils.validateUsernameSearchQuery(
          req.query.username
        );
        if (usernameSearchError) {
          errors.push({ field: "username", message: usernameSearchError });
        }
      }

      if (req.query.role) {
        const roleError = validationUtils.validateUserRole(req.query.role);
        if (roleError) {
          errors.push({ field: "role", message: roleError });
        }
      }

      if (req.query.is_active !== undefined) {
        if (!["true", "false"].includes(req.query.is_active.toLowerCase())) {
          errors.push({
            field: "is_active",
            message: 'is_active must be "true" or "false".',
          });
        }
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 10;
      const search = req.query.username || "";
      const role = req.query.role || null;
      const is_active =
        req.query.is_active !== undefined
          ? req.query.is_active === "true"
          : null;

      const result = await userService.getAllUsers(
        page,
        limit,
        search,
        role,
        is_active
      );

      logger.debug(
        `Retrieved ${result.users.length} users (total: ${result.total}) with filters:`,
        {
          page,
          limit,
          search,
          role,
          is_active,
          requestedBy: req.user?.id,
        }
      );

      return res.status(200).json({
        status: "success",
        data: result,
      });
    } catch (error) {
      logger.error("Failed to fetch users:", {
        error: error.message,
        stack: error.stack,
      });

      return res.status(500).json({
        status: "error",
        message: error.message || "Failed to fetch users",
      });
    }
  }

  /**
   * Get user by ID
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.userId - User ID to retrieve
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - User object with:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {string} - When user was created (ISO-8601 format)
   *     - updated_at {string} - When user was last updated (ISO-8601 format)
   *   - message {string} - Error message (only if status is 'error')
   */
  async getUserById(req, res) {
    try {
      const id = parseInt(req.params.userId);

      if (!id) {
        logger.debug("Invalid user ID provided:", { id: req.params.userId });
        return res.status(400).json({
          status: "error",
          message: "User ID is required",
        });
      }

      logger.debug(`Fetching user with ID: ${id}`);
      const user = await userService.getUserById(id);

      return res.status(200).json({
        status: "success",
        data: user,
      });
    } catch (error) {
      logger.error(`Failed to get user with ID ${req.params.userId}:`, {
        error: error.message,
        stack: error.stack,
      });

      return res.status(404).json({
        status: "error",
        message: error.message || "User not found",
      });
    }
  }

  /**
   * Create a new user
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.username - Username for the new user
   * @param {string} req.body.password - Password for the new user
   * @param {string} [req.body.role='user'] - Role for the new user (admin/user)
   * @param {boolean} [req.body.is_active=true] - Whether the new user is active
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Created user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {string} - When user was created (ISO-8601 format)
   *     - updated_at {string} - When user was last updated (ISO-8601 format)
   *   - message {string} - Success or error message
   */
  async createUser(req, res) {
    try {
      const { username, password, role, is_active } = req.body;
      const errors = [];

      if (!username || !password) {
        logger.debug("Missing required user creation fields:", {
          hasUsername: !!username,
          hasPassword: !!password,
        });

        return res.status(400).json({
          status: "error",
          message: "Username and password are required",
        });
      }

      const usernameError = validationUtils.validateUsername(username);
      if (usernameError) {
        errors.push({ field: "username", message: usernameError });
      }

      const passwordError = validationUtils.validatePassword(password);
      if (passwordError) {
        errors.push({ field: "password", message: passwordError });
      }

      if (role) {
        const roleError = validationUtils.validateUserRole(role);
        if (roleError) {
          errors.push({ field: "role", message: roleError });
        }
      }

      if (is_active !== undefined) {
        const isActiveError = validationUtils.validateIsActiveFlag(is_active);
        if (isActiveError) {
          errors.push({ field: "is_active", message: isActiveError });
        }
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const userData = {
        username,
        password,
        role,
        is_active,
      };
      const user = await userService.createUser(userData);

      logger.info(
        `User created successfully: ID ${user.id}, username: ${user.username}`
      );

      return res.status(201).json({
        status: "success",
        data: user,
        message: "User created successfully",
      });
    } catch (error) {
      logger.error("Failed to create user:", {
        error: error.message,
        stack: error.stack,
        username: req.body.username,
      });

      return res.status(400).json({
        status: "error",
        message: error.message || "Failed to create user",
      });
    }
  }

  /**
   * Update a user
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.userId - User ID to update
   * @param {Object} req.body - Request body
   * @param {string} [req.body.role] - New role for the user
   * @param {boolean} [req.body.is_active] - New active status for the user
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Updated user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active
   *     - created_at {string} - When user was created (ISO-8601 format)
   *     - updated_at {string} - When user was last updated (ISO-8601 format)
   *   - message {string} - Success or error message
   */
  async updateUser(req, res) {
    try {
      const id = parseInt(req.params.userId);
      const { role, is_active } = req.body;
      const errors = [];

      if (!id) {
        logger.debug("Invalid user ID provided for update:", {
          id: req.params.userId,
        });
        return res.status(400).json({
          status: "error",
          message: "User ID is required",
        });
      }

      if (req.body.username || req.body.password) {
        logger.warn(
          "Attempt to update username/password via unauthorized endpoint:",
          {
            userId: id,
            attemptedBy: req.user?.id,
          }
        );

        return res.status(400).json({
          status: "error",
          message: "Username and password cannot be updated via this endpoint",
        });
      }

      if (role !== undefined) {
        const roleError = validationUtils.validateUserRole(role);
        if (roleError) {
          errors.push({ field: "role", message: roleError });
        }
      }

      if (is_active !== undefined) {
        const isActiveError = validationUtils.validateIsActiveFlag(is_active);
        if (isActiveError) {
          errors.push({ field: "is_active", message: isActiveError });
        }
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const userData = {};

      if (role !== undefined) userData.role = role;
      if (is_active !== undefined) userData.is_active = is_active;

      const user = await userService.updateUser(id, userData);

      logger.info(`User ID ${id} updated successfully`);

      return res.status(200).json({
        status: "success",
        data: user,
        message: "User updated successfully",
      });
    } catch (error) {
      logger.error(`Failed to update user ID ${req.params.userId}:`, {
        error: error.message,
        stack: error.stack,
      });

      return res.status(400).json({
        status: "error",
        message: error.message || "Failed to update user",
      });
    }
  }

  /**
   * Delete/inactivate a user
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.userId - User ID to delete/inactivate
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
   */
  async deleteUser(req, res) {
    try {
      const id = parseInt(req.params.userId);

      if (!id) {
        logger.debug("Invalid user ID provided for deletion:", {
          id: req.params.userId,
        });
        return res.status(400).json({
          status: "error",
          message: "User ID is required",
        });
      }

      await userService.deleteUser(id);

      logger.info(`User ID ${id} inactivated successfully`);

      return res.status(200).json({
        status: "success",
        message: "User inactivated successfully",
      });
    } catch (error) {
      logger.error(`Failed to inactivate user ID ${req.params.userId}:`, {
        error: error.message,
        stack: error.stack,
      });

      return res.status(404).json({
        status: "error",
        message: error.message || "User not found",
      });
    }
  }

  /**
   * Reactivate an inactive user
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.userId - User ID to reactivate
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Reactivated user object:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role (admin/user)
   *     - is_active {boolean} - Whether user is active (true after reactivation)
   *     - created_at {string} - When user was created (ISO-8601 format)
   *     - updated_at {string} - When user was last updated (ISO-8601 format)
   *   - message {string} - Success or error message
   */
  async reactivateUser(req, res) {
    try {
      const id = parseInt(req.params.userId);

      if (!id) {
        logger.debug("Invalid user ID provided for reactivation:", {
          id: req.params.userId,
        });
        return res.status(400).json({
          status: "error",
          message: "User ID is required",
        });
      }

      const user = await userService.reactivateUser(id);

      logger.info(`User ID ${id} reactivated successfully`);

      return res.status(200).json({
        status: "success",
        data: user,
        message: "User reactivated successfully",
      });
    } catch (error) {
      logger.error(`Failed to reactivate user ID ${req.params.userId}:`, {
        error: error.message,
        stack: error.stack,
      });

      return res.status(404).json({
        status: "error",
        message: error.message || "User not found",
      });
    }
  }
}

module.exports = new UserController();
