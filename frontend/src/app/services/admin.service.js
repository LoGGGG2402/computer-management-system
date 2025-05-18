import api from './api';

/**
 * Service for admin-related functionality including system statistics and agent version management.
 */
class AdminService {
  /**
   * Get system-wide statistics.
   * Requires admin privileges.
   * @returns {Promise<Object>} System statistics object with:
   *   - totalUsers {number} - Total number of users in the system
   *   - totalRooms {number} - Total number of rooms in the system
   *   - totalComputers {number} - Total number of computers in the system
   *   - onlineComputers {number} - Number of computers currently online
   *   - offlineComputers {number} - Number of computers currently offline
   *   - computersWithErrors {number} - Number of computers with active errors
   *   - unresolvedErrors {Array<Object>} - List of unresolved errors with details:
   *     - computerId {number} - ID of the computer with the error
   *     - computerName {string} - Name of the computer with the error
   *     - errorId {number} - ID of the error
   *     - error_type {string} - Type of the error
   *     - error_message {string} - Human-readable error message
   *     - error_details {Object} - Additional details about the error
   *     - reported_at {string} - When the error was reported (ISO-8601 format)
   * @throws {Error} If fetching statistics fails.
   */
  async getSystemStats() {
    try {
      const response = await api.get('/admin/stats');
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch system statistics';
      console.error('Get system stats error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get all agent versions
   * @returns {Promise<Array<Object>>} List of agent versions with the following properties for each:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string (e.g., '1.0.0')
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes describing changes and features
   *   - is_stable {boolean} - Flag indicating if this is a stable production release
   *   - file_path {string} - Server file path
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *   - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   * @throws {Error} If fetching agent versions fails.
   */
  async getAgentVersions() {
    try {
      const response = await api.get('/admin/agents/versions');
      return response.data.status === 'success' ? response.data.data : [];
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch agent versions';
      console.error('Get agent versions error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Upload a new agent package version
   * @param {FormData} formData - Form data containing:
   *   - package {File} - The agent package file to upload
   *   - version {string} - Semantic version string (e.g., '1.0.0')
   *   - notes {string} - Release notes for this version
   * @returns {Promise<Object>} Created AgentVersion object with:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes
   *   - is_stable {boolean} - Flag indicating if this is a stable release (initially false)
   *   - file_path {string} - Server file path
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *   - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   * @throws {Error} If uploading the agent package fails.
   */
  async uploadAgentPackage(formData) {
    try {
      const response = await api.post('/admin/agents/versions', formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      });
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to upload agent package';
      console.error('Upload agent package error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Update agent version stability flag
   * @param {string} versionId - The agent version UUID to update
   * @param {boolean} isStable - Whether to mark this version as stable
   * @returns {Promise<Object>} Updated AgentVersion object with:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes
   *   - is_stable {boolean} - Flag indicating if this is a stable release (updated)
   *   - file_path {string} - Server file path
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *   - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   * @throws {Error} If updating the agent version stability fails.
   */
  async updateAgentVersionStability(versionId, isStable) {
    try {
      const response = await api.put(`/admin/agents/versions/${versionId}`, {
        is_stable: isStable
      });
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update agent version stability';
      console.error('Update agent version stability error:', errorMessage);
      throw new Error(errorMessage);
    }
  }
}

export default new AdminService();