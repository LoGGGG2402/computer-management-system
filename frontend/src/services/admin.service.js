import api from './api';

/**
 * Service for administrator-related functions,
 * including system statistics and agent version management.
 * All endpoints in this service require admin privileges.
 */
class AdminService {
  /**
   * Get system-wide statistics.
   * Requires admin privileges.
   * @returns {Promise<Object>} System statistics object.
   * API Doc Response: { status: "success", data: { totalUsers, totalRooms, ..., unresolvedErrors: [...] } }
   * @throws {Error} If fetching statistics fails.
   */
  async getSystemStats() {
    try {
      const response = await api.get('/admin/stats');
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch system statistics.';
      console.error('Error fetching system statistics:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Get list of all agent versions.
   * Requires admin privileges.
   * @returns {Promise<Array<Object>>} List of agent versions.
   * API Doc Response: { status: "success", data: [ { id, version, checksum_sha256, ... }, ... ] }
   * @throws {Error} If fetching agent versions fails.
   */
  async getAgentVersions() {
    try {
      const response = await api.get('/admin/agents/versions');
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch agent versions.';
      console.error('Error fetching agent versions:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }

  /**
   * Upload a new agent package
   * @param {FormData} formData - FormData containing package file and metadata
   * @returns {Promise<Object>} Created agent version
   */
  async uploadAgentPackage(formData) {
    try {
      const response = await api.post('/admin/agents/versions', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });
      return response.data;
    } catch (error) {
      console.error('Error uploading agent package:', error);
      throw error;
    }
  }

  /**
   * Update stability flag (is_stable) of an agent version.
   * Requires admin privileges.
   * @param {string} versionId - UUID of agent version to update.
   * @param {boolean} isStable - New stability status (true or false).
   * @returns {Promise<Object>} Updated agent version data.
   * API Doc Response: { status: "success", message, data: { id, version, ..., is_stable, ... } }
   * @throws {Error} If updating stability status fails.
   */
  async updateAgentVersionStability(versionId, isStable) {
    try {
      const response = await api.put(`/admin/agents/versions/${versionId}`, {
        is_stable: isStable,
      });
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to update agent version stability status.';
      console.error('Error updating agent version stability:', errorMessage, error);
      throw new Error(errorMessage);
    }
  }
}

export default new AdminService();
