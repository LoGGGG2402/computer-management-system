import api from './api';

/**
 * Service for user management operations
 */
class UserService {
  /**
   * Get all users with pagination and filtering
   * @param {Object} filters - Filter parameters
   * @param {number} [filters.page=1] - Page number
   * @param {number} [filters.limit=10] - Items per page
   * @param {string} [filters.username] - Filter by username (partial match)
   * @param {string} [filters.role] - Filter by role ('admin'/'user')
   * @param {boolean|string} [filters.is_active] - Filter by active status
   * @returns {Promise<Object>} Paginated users data with:
   *   - total {number} - Total number of users matching criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - users {Array<Object>} - Array of user objects:
   *     - id {number} - User ID
   *     - username {string} - Username
   *     - role {string} - User role ('admin'/'user')
   *     - is_active {boolean} - Whether user is active
   *     - created_at {Date} - When user was created
   *     - updated_at {Date} - When user was last updated
   * @throws {Error} If fetching users fails
   */
  async getAllUsers(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', filters.page);
      if (filters.limit) params.append('limit', filters.limit);
      if (filters.username) params.append('username', filters.username);
      if (filters.role) params.append('role', filters.role);
      if (filters.is_active !== undefined) params.append('is_active', filters.is_active);
      
      const response = await api.get(`/users?${params.toString()}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch users';
      console.error('Get users error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get user by ID
   * @param {number} id - User ID to retrieve
   * @returns {Promise<Object>} User data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin'/'user')
   *   - is_active {boolean} - Whether user is active
   *   - created_at {Date} - When user was created
   *   - updated_at {Date} - When user was last updated
   * @throws {Error} If user is not found
   */
  async getUserById(id) {
    try {
      const response = await api.get(`/users/${id}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user';
      console.error('Get user error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Create a new user
   * @param {Object} userData - User data
   * @param {string} userData.username - Username for the new user
   * @param {string} userData.password - Password for the new user
   * @param {string} [userData.role='user'] - Role for the new user
   * @param {boolean} [userData.is_active=true] - Whether the new user is active
   * @returns {Promise<Object>} Created user data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin'/'user')
   *   - is_active {boolean} - Whether user is active
   *   - created_at {Date} - When user was created
   *   - updated_at {Date} - When user was last updated
   * @throws {Error} If creating user fails
   */
  async createUser(userData) {
    try {
      const response = await api.post(`/users`, userData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to create user';
      console.error('Create user error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Update a user
   * @param {number} id - User ID to update
   * @param {Object} userData - User data to update
   * @param {string} [userData.role] - New role for the user
   * @param {boolean} [userData.is_active] - New active status for the user
   * @returns {Promise<Object>} Updated user data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin'/'user')
   *   - is_active {boolean} - Whether user is active
   *   - created_at {Date} - When user was created
   *   - updated_at {Date} - When user was last updated
   * @throws {Error} If updating user fails
   */
  async updateUser(id, userData) {
    try {
      const response = await api.put(`/users/${id}`, userData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update user';
      console.error('Update user error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Delete/inactivate a user
   * @param {number} id - User ID to delete/inactivate
   * @returns {Promise<boolean>} Success status
   * @throws {Error} If deleting user fails
   */
  async deleteUser(id) {
    try {
      await api.delete(`/users/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to delete user';
      console.error('Delete user error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Reactivate an inactive user
   * @param {number} id - User ID to reactivate
   * @returns {Promise<Object>} Reactivated user data with:
   *   - id {number} - User ID
   *   - username {string} - Username
   *   - role {string} - User role ('admin'/'user')
   *   - is_active {boolean} - Whether user is active (true after reactivation)
   *   - created_at {Date} - When user was created
   *   - updated_at {Date} - When user was last updated
   * @throws {Error} If reactivating user fails
   */
  async reactivateUser(id) {
    try {
      const response = await api.put(`/users/${id}/reactivate`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to reactivate user';
      console.error('Reactivate user error:', errorMessage);
      throw new Error(errorMessage);
    }
  }
}

export default new UserService();