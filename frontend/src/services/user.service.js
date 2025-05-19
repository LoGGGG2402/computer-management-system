import api from './api';

/**
 * Service for user management operations.
 * All endpoints in this service require admin privileges,
 * based on API documentation (`<admin_jwt_token_string>`).
 */
class UserService {
  /**
   * Get list of all users with pagination and filters.
   * Requires admin privileges.
   * @param {Object} [filters={}] - Filter parameters.
   * @param {number} [filters.page=1] - Page number.
   * @param {number} [filters.limit=10] - Items per page.
   * @param {string} [filters.username] - Filter by username (fuzzy search).
   * @param {string} [filters.role] - Filter by role ('admin'/'user').
   * @param {boolean|string} [filters.is_active] - Filter by active status.
   * @returns {Promise<Object>} Paginated user data.
   * API Doc Response: { status: "success", data: { total, currentPage, totalPages, users: [...] } }
   * @throws {Error} If fetching user list fails.
   */
  async getAllUsers(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', String(filters.page));
      if (filters.limit) params.append('limit', String(filters.limit));
      if (filters.username) params.append('username', filters.username);
      if (filters.role) params.append('role', filters.role);
      if (filters.is_active !== undefined) params.append('is_active', String(filters.is_active));
      const response = await api.get(`/users?${params.toString()}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user list.';
      console.error('Error fetching user list:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get detailed user information by ID.
   * Requires admin privileges.
   * @param {number|string} id - User ID.
   * @returns {Promise<Object>} User data.
   * API Doc Response: { status: "success", data: { id, username, role, ... } }
   * @throws {Error} If user not found or error occurs.
   */
  async getUserById(id) {
    try {
      const response = await api.get(`/users/${id}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user information.';
      console.error('Error fetching user by ID:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Create a new user.
   * Requires admin privileges.
   * @param {Object} userData - User data.
   * @param {string} userData.username - Username (required).
   * @param {string} userData.password - Password (required).
   * @param {string} [userData.role='user'] - Role ('admin' or 'user').
   * @param {boolean} [userData.is_active=true] - Active status.
   * @returns {Promise<Object>} Created user data.
   * API Doc Response: { status: "success", data: { id, username, ... }, message }
   * @throws {Error} If user creation fails.
   */
  async createUser(userData) {
    try {
      const response = await api.post('/users', userData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to create user.';
      console.error('Error creating user:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Update user information.
   * Requires admin privileges. Only role and is_active can be updated according to API doc.
   * @param {number|string} id - ID of user to update.
   * @param {Object} userData - Update data.
   * @param {string} [userData.role] - New role.
   * @param {boolean} [userData.is_active] - New active status.
   * @returns {Promise<Object>} Updated user data.
   * API Doc Response: { status: "success", data: { id, username, ... }, message }
   * @throws {Error} If user update fails.
   */
  async updateUser(id, userData) {
    try {
      const validUpdateData = {};
      if (userData.hasOwnProperty('role')) {
        validUpdateData.role = userData.role;
      }
      if (userData.hasOwnProperty('is_active')) {
        validUpdateData.is_active = userData.is_active;
      }

      const response = await api.put(`/users/${id}`, validUpdateData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update user.';
      console.error('Error updating user:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Deactivate a user.
   * Requires admin privileges.
   * @param {number|string} id - ID of user to deactivate.
   * @returns {Promise<boolean>} True if successful.
   * API Doc Response: { status: "success", message: "User deactivated successfully" }
   * @throws {Error} If deactivation fails.
   */
  async deactivateUser(id) {
    try {
      await api.delete(`/users/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to deactivate user.';
      console.error('Error deactivating user:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Reactivate a previously deactivated user.
   * Requires admin privileges.
   * @param {number|string} id - ID of user to reactivate.
   * @returns {Promise<Object>} Reactivated user data.
   * API Doc Response: { status: "success", data: { id, username, ..., is_active: true }, message }
   * @throws {Error} If reactivation fails.
   */
  async reactivateUser(id) {
    try {
      const response = await api.put(`/users/${id}/reactivate`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to reactivate user.';
      console.error('Error reactivating user:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }
}

export default new UserService();
