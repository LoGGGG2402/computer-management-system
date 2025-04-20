const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const db = require('../database/models');
const websocketService = require('./websocket.service');
const { User, Room, Computer, AgentVersion } = db;

class AdminService {
  /**
   * Retrieves general statistics about the system.
   * This includes counts of users, rooms, computers, online/offline status,
   * computers with errors, and a list of unresolved errors.
   *
   * @returns {Promise<object>} A promise that resolves to an object containing system statistics:
   *   - `totalUsers` {number}: The total number of registered users.
   *   - `totalRooms` {number}: The total number of rooms.
   *   - `totalComputers` {number}: The total number of computers registered in the system.
   *   - `onlineComputers` {number}: The number of computers currently connected via WebSocket.
   *   - `offlineComputers` {number}: The calculated number of computers not currently connected (total - online).
   *   - `computersWithErrors` {number}: The number of computers flagged with active errors.
   *   - `unresolvedErrors` {Array<Object>}: A list of currently unresolved errors across all computers. Each object contains:
   *     - `computerId` {number}: ID of the computer with the error.
   *     - `computerName` {string}: Name of the computer with the error.
   *     - `errorId` {number}: Unique ID of the error instance.
   *     - `error_type` {string}: Type/category of the error.
   *     - `error_message` {string}: Human-readable error message.
   *     - `error_details` {Object}: Additional details about the error.
   *     - `reported_at` {Date}: When the error was reported.
   * @throws {Error} Throws an error if there's an issue querying the database
   *                 or retrieving the number of connected agents from the WebSocket service.
   *                 The error message will be 'Could not retrieve statistics data.'.
   */
  async getSystemStats() {
    try {
      const [
        totalUsers,
        totalRooms,
        totalComputers,
        computersWithErrorRecords,
      ] = await Promise.all([
        User.count(),
        Room.count(),
        Computer.count(),
        Computer.findAll({
          where: { have_active_errors: true },
          attributes: ['id', 'name', 'errors'] 
        }),
      ]);

      const unresolvedErrors = [];
      computersWithErrorRecords.forEach(computer => {
        const errors = Array.isArray(computer.errors) ? computer.errors : [];
        errors.forEach(error => {
          if (!error.resolved) {
            unresolvedErrors.push({
              computerId: computer.id,
              computerName: computer.name,
              errorId: error.id, 
              error_type: error.error_type,
              error_message: error.error_message,
              error_details: error.error_details,
              reported_at: error.reported_at,
            });
          }
        });
      });

      const onlineComputers = websocketService.numberOfConnectedAgents();
      const computersWithErrorsCount = computersWithErrorRecords.length;

      return {
        totalUsers,
        totalRooms,
        totalComputers,
        onlineComputers,
        offlineComputers: totalComputers - onlineComputers,
        computersWithErrors: computersWithErrorsCount, 
        unresolvedErrors,
      };
    } catch (error) {
      throw error; // Pass through the original error
    }
  }

  /**
   * Process an uploaded agent package
   * @param {Object} file - The uploaded file object from multer
   * @param {string} file.path - Path where the uploaded file was stored
   * @param {number} file.size - File size in bytes
   * @param {Object} versionData - Version metadata
   * @param {string} versionData.version - Semantic version string (e.g., '1.0.0')
   * @param {string} [versionData.notes=''] - Release notes describing changes and features
   * @returns {Promise<Object>} Created AgentVersion record with the following properties:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string (e.g., '1.0.0')
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package for integrity verification
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes describing changes and features
   *   - is_stable {boolean} - Flag indicating if this is a stable production release (initially false)
   *   - file_path {string} - Server filesystem path where the agent package is stored
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {Date} - Timestamp when this version was created
   *   - updated_at {Date} - Timestamp when this version was last modified
   * @throws {Error} If file is invalid or version already exists
   */
  async processAgentUpload(file, versionData) {
    try {
      if (!file || !file.path) {
        throw new Error('No valid file provided');
      }

      if (!versionData.version) {
        throw new Error('Version is required');
      }

      // Calculate SHA-256 checksum
      const fileBuffer = fs.readFileSync(file.path);
      const hashSum = crypto.createHash('sha256');
      hashSum.update(fileBuffer);
      const checksum = hashSum.digest('hex');

      // Create download URL path (relative to API base)
      const filename = path.basename(file.path);
      const downloadUrl = `/api/agent/agent-packages/${filename}`;

      // Create agent version record
      const agentVersion = await AgentVersion.create({
        version: versionData.version,
        checksum_sha256: checksum,
        download_url: downloadUrl,
        notes: versionData.notes || '',
        is_stable: false, // Default to not stable (must be explicitly set later)
        file_path: file.path,
        file_size: file.size
      });

      return agentVersion;
    } catch (error) {
      // If file exists but agent version creation failed, clean up the file
      if (file && file.path && fs.existsSync(file.path)) {
        try {
          fs.unlinkSync(file.path);
        } catch (cleanupError) {
          // Let the controller handle the logging
        }
      }
      
      throw error; // Pass through the original error
    }
  }

  /**
   * Update stability flag of an agent version
   * @param {string} versionId - The agent version ID (UUID)
   * @param {boolean} isStable - Whether to mark as stable
   * @returns {Promise<Object>} Updated AgentVersion object with the following properties:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string (e.g., '1.0.0')
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes describing changes and features
   *   - is_stable {boolean} - Flag indicating if this is a stable production release (updated)
   *   - file_path {string} - Server filesystem path where the agent package is stored
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {Date} - Timestamp when this version was created
   *   - updated_at {Date} - Timestamp when this version was last modified
   * @throws {Error} If version with given ID does not exist
   */
  async updateStabilityFlag(versionId, isStable) {
    try {
      const agentVersion = await AgentVersion.findByPk(versionId);

      if (!agentVersion) {
        throw new Error(`Agent version with ID ${versionId} not found`);
      }

      if (isStable) {
        // If setting to stable, mark all other versions as not stable
        await AgentVersion.update(
          { is_stable: false },
          { where: { id: { [require('sequelize').Op.ne]: versionId } } }
        );
      }

      // Update the version
      await agentVersion.update({ is_stable: isStable });

      return agentVersion;
    } catch (error) {
      throw error; // Pass through the original error
    }
  }

  /**
   * Get all agent versions
   * @returns {Promise<Array<Object>>} List of all agent versions ordered by stability and creation date
   * @returns {Promise<Array<AgentVersion>>} List of all agent versions with the following properties for each:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string (e.g., '1.0.0') 
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package
   *   - download_url {string} - URL path where agents can download this version
   *   - notes {string|null} - Release notes describing changes and features
   *   - is_stable {boolean} - Flag indicating if this is a stable production release
   *   - file_path {string} - Server filesystem path where the agent package is stored
   *   - file_size {number} - Size of the agent package file in bytes
   *   - created_at {Date} - Timestamp when this version was created
   *   - updated_at {Date} - Timestamp when this version was last modified
   */
  async getAllVersions() {
    try {
      const versions = await AgentVersion.findAll({
        order: [
          ['is_stable', 'DESC'],
          ['created_at', 'DESC']
        ]
      });
      
      return versions;
    } catch (error) {
      throw error; // Pass through the original error
    }
  }
}

module.exports = new AdminService();