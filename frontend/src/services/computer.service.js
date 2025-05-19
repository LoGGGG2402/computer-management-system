import api from './api';

/**
 * Service for computer management operations.
 */
class ComputerService {
  /**
   * Get list of all computers with pagination and filters.
   * Requires JWT token.
   * @param {Object} [filters={}] - Filter parameters.
   * @param {number} [filters.page=1] - Page number.
   * @param {number} [filters.limit=10] - Items per page.
   * @param {string} [filters.name] - Filter by computer name (fuzzy search).
   * @param {number|string} [filters.roomId] - Filter by room ID.
   * @param {string} [filters.status] - Filter by status ('online'/'offline').
   * @param {boolean|string} [filters.has_errors] - Filter computers with errors.
   * @returns {Promise<Object>} Paginated computer data.
   * API Doc Response: { status: "success", data: { total, currentPage, totalPages, computers: [...] } }
   * @throws {Error} If fetching computer list fails.
   */
  async getAllComputers(filters = {}) {
    try {
      const params = new URLSearchParams();
      if (filters.page) params.append('page', String(filters.page));
      if (filters.limit) params.append('limit', String(filters.limit));
      if (filters.name) params.append('name', filters.name);
      if (filters.roomId) params.append('roomId', String(filters.roomId));
      if (filters.status) params.append('status', filters.status);
      if (filters.has_errors !== undefined) params.append('has_errors', String(filters.has_errors));

      const response = await api.get(`/computers?${params.toString()}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computer list.';
      console.error('Error fetching computer list:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get detailed computer information by ID.
   * Requires JWT token.
   * @param {number|string} id - Computer ID.
   * @returns {Promise<Object>} Detailed computer data.
   * API Doc Response: { status: "success", data: { id, name, status, ..., room: { id, name } } }
   * @throws {Error} If computer not found or error occurs.
   */
  async getComputerById(id) {
    try {
      const response = await api.get(`/computers/${id}`);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computer information.';
      console.error('Error fetching computer by ID:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Delete a computer.
   * Requires admin privileges.
   * @param {number|string} id - ID of computer to delete.
   * @returns {Promise<boolean>} True if successful.
   * API Doc Response: { status: "success", message: "Computer deleted successfully" }
   * @throws {Error} If computer deletion fails.
   */
  async deleteComputer(id) {
    try {
      await api.delete(`/computers/${id}`);
      return true;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to delete computer.';
      console.error('Error deleting computer:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get list of errors for a specific computer.
   * Requires JWT token.
   * @param {number|string} computerId - Computer ID.
   * @returns {Promise<Array<Object>>} Array of error objects.
   * API Doc Response: { status: "success", data: { errors: [...] } }
   * @throws {Error} If fetching error list fails.
   */
  async getComputerErrors(computerId) {
    try {
      const response = await api.get(`/computers/${computerId}/errors`);
      return response.data.data.errors;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch computer errors.';
      console.error('Error fetching computer errors:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Report a new error for a computer.
   * Requires JWT token.
   * @param {number|string} computerId - Computer ID.
   * @param {Object} errorData - Error data.
   * @param {string} errorData.error_type - Error type (required, enum: "hardware", "software", "network", "peripheral", "other").
   * @param {string} errorData.error_message - Error message (required).
   * @param {Object} [errorData.error_details={}] - Additional error details.
   * @returns {Promise<Object>} Reported error data.
   * API Doc Response: { status: "success", data: { error: {...}, computerId: ... }, message }
   * @throws {Error} If error reporting fails.
   */
  async reportComputerError(computerId, errorData) {
    try {
      const response = await api.post(`/computers/${computerId}/errors`, errorData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to report computer error.';
      console.error('Error reporting computer error:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Resolve a computer error.
   * Requires JWT token.
   * @param {number|string} computerId - Computer ID.
   * @param {number|string} errorId - ID of error to resolve.
   * @param {Object} resolutionData - Resolution data.
   * @param {string} resolutionData.resolution_notes - Resolution notes (required).
   * @returns {Promise<Object>} Updated error data.
   * API Doc Response: { status: "success", data: { error: {...}, computerId: ... }, message }
   * @throws {Error} If error resolution fails.
   */
  async resolveComputerError(computerId, errorId, resolutionData) {
    try {
      const response = await api.put(`/computers/${computerId}/errors/${errorId}/resolve`, resolutionData);
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to resolve computer error.';
      console.error('Error resolving computer error:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }
}

export default new ComputerService();
