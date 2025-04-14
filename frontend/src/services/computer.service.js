import api from './api';

/**
 * Service for computer management operations
 */
class ComputerService {
  /**
   * Get all computers with pagination and filters
   * @param {Object} filters - Filter parameters
   * @param {number} [filters.page=1] - Page number
   * @param {number} [filters.limit=10] - Items per page
   * @param {string} [filters.name] - Filter by computer name (partial match)
   * @param {number} [filters.roomId] - Filter by room ID
   * @param {string} [filters.status] - Filter by status ('online'/'offline')
   * @param {boolean|string} [filters.has_errors] - Filter to show only computers with errors
   * @returns {Promise<Object>} Paginated computers data with:
   *   - total {number} - Total number of computers matching criteria
   *   - currentPage {number} - Current page number
   *   - totalPages {number} - Total number of pages
   *   - computers {Array<Object>} - Array of computer objects:
   *     - id {number} - Computer ID
   *     - name {string} - Computer name
   *     - status {string} - Computer status ('online'/'offline')
   *     - have_active_errors {boolean} - Whether computer has active errors
   *     - last_update {Date} - When computer was last updated
   *     - room_id {number} - ID of the room this computer belongs to
   *     - pos_x {number} - X position in room grid
   *     - pos_y {number} - Y position in room grid
   *     - os_info {Object} - Operating system information
   *     - cpu_info {Object} - CPU information
   *     - gpu_info {Object} - GPU information
   *     - total_ram {number} - Total RAM in GB
   *     - total_disk_space {number} - Total disk space in GB
   *     - room {Object} - Associated room information:
   *       - id {number} - Room ID
   *       - name {string} - Room name
   * @throws {Error} If fetching computers fails
   */
  async getAllComputers(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', filters.page);
      if (filters.limit) params.append('limit', filters.limit);
      if (filters.name) params.append('name', filters.name);
      if (filters.roomId) params.append('roomId', filters.roomId);
      if (filters.status) params.append('status', filters.status);
      if (filters.has_errors !== undefined) params.append('has_errors', filters.has_errors);
      
      const response = await api.get(`/computers?${params.toString()}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computers';
      console.error('Get computers error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get computer by ID
   * @param {number} id - Computer ID to retrieve
   * @returns {Promise<Object>} Computer data with:
   *   - id {number} - Computer ID
   *   - name {string} - Computer name
   *   - status {string} - Computer status ('online'/'offline')
   *   - have_active_errors {boolean} - Whether computer has active errors
   *   - last_update {Date} - When computer was last updated
   *   - room_id {number} - ID of the room this computer belongs to
   *   - pos_x {number} - X position in room grid
   *   - pos_y {number} - Y position in room grid
   *   - os_info {Object} - Operating system information
   *   - cpu_info {Object} - CPU information
   *   - gpu_info {Object} - GPU information
   *   - total_ram {number} - Total RAM in GB
   *   - total_disk_space {number} - Total disk space in GB
   *   - room {Object} - Associated room information:
   *     - id {number} - Room ID
   *     - name {string} - Room name
   * @throws {Error} If computer is not found
   */
  async getComputerById(id) {
    try {
      const response = await api.get(`/computers/${id}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computer';
      console.error('Get computer error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Delete a computer
   * @param {number} id - Computer ID to delete
   * @returns {Promise<boolean>} Success status
   * @throws {Error} If deleting computer fails
   */
  async deleteComputer(id) {
    try {
      await api.delete(`/computers/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to delete computer';
      console.error('Delete computer error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get errors for a specific computer
   * @param {number} id - Computer ID to get errors for
   * @returns {Promise<Object[]>} Array of error objects:
   *   - id {number} - Error ID
   *   - error_type {string} - Type/category of the error
   *   - error_message {string} - Human-readable error message
   *   - error_details {Object} - Additional details about the error
   *   - reported_at {Date} - When the error was reported
   *   - resolved {boolean} - Whether the error has been resolved
   *   - resolved_at {Date} - When the error was resolved (if applicable)
   *   - resolution_notes {string} - Notes about how the error was resolved
   * @throws {Error} If fetching errors fails
   */
  async getComputerErrors(id) {
    try {
      const response = await api.get(`/computers/${id}/errors`);
      return response.data.data.errors;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computer errors';
      console.error('Get computer errors error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Report a new error for a computer
   * @param {number} id - Computer ID to report error for
   * @param {Object} errorData - Error data
   * @param {string} errorData.error_type - Type/category of the error
   * @param {string} errorData.error_message - Human-readable error message
   * @param {Object} [errorData.error_details={}] - Additional details about the error
   * @returns {Promise<Object>} Result object with:
   *   - error {Object} - The created error object:
   *     - id {number} - Error ID
   *     - error_type {string} - Type/category of the error
   *     - error_message {string} - Human-readable error message
   *     - error_details {Object} - Additional details about the error
   *     - reported_at {Date} - When the error was reported
   *     - resolved {boolean} - Whether the error has been resolved (false for new errors)
   *   - computerId {number} - The computer ID associated with this error
   * @throws {Error} If reporting error fails
   */
  async reportComputerError(id, errorData) {
    try {
      const response = await api.post(`/computers/${id}/errors`, errorData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to report computer error';
      console.error('Report computer error error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Resolve a computer error
   * @param {number} computerId - Computer ID
   * @param {number} errorId - Error ID to resolve
   * @param {Object} data - Resolution data
   * @param {string} [data.resolution_notes] - Notes about how the error was resolved
   * @returns {Promise<Object>} Result object with:
   *   - error {Object} - The updated error object:
   *     - id {number} - Error ID
   *     - error_type {string} - Type/category of the error
   *     - error_message {string} - Human-readable error message
   *     - error_details {Object} - Additional details about the error
   *     - reported_at {Date} - When the error was reported
   *     - resolved {boolean} - Whether the error has been resolved (true after resolution)
   *     - resolved_at {Date} - When the error was resolved
   *     - resolution_notes {string} - Notes about how the error was resolved
   *   - computerId {number} - The computer ID associated with this error
   * @throws {Error} If resolving error fails
   */
  async resolveComputerError(computerId, errorId, data = {}) {
    try {
      const response = await api.put(`/computers/${computerId}/errors/${errorId}/resolve`, data);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to resolve computer error';
      console.error('Resolve computer error error:', errorMessage);
      throw new Error(errorMessage);
    }
  }
}

export default new ComputerService();