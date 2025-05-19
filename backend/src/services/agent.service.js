const { AgentVersion } = require('../database/models');
const semver = require('semver');

/**
 * Service for agent-related operations, including version management
 */
class AgentService {
  /**
   * Get the latest stable agent version info
   * @param {string} currentVersion - Current version of the requesting agent
   * @returns {Promise<Object|null>} Version info if newer version available, null otherwise
   * @returns {Object} AgentVersion info object with the following properties:
   *   - version {string} - Semantic version string (e.g., '1.2.3')
   *   - download_url {string} - URL path where agent can download this version
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package for integrity verification
   *   - notes {string|null} - Release notes describing changes and features
   */
  async getLatestStableVersionInfo(currentVersion) {
    try {
      const latestStableVersion = await AgentVersion.findOne({
        where: { is_stable: true },
        order: [['created_at', 'DESC']]
      });

      if (!latestStableVersion) {
        return null;
      }

      // If no current version provided or if latest version is newer
      if (!currentVersion || semver.gt(latestStableVersion.version, currentVersion)) {
        return {
          version: latestStableVersion.version,
          download_url: latestStableVersion.download_url,
          checksum_sha256: latestStableVersion.checksum_sha256,
          notes: latestStableVersion.notes
        };
      }

      return null;
    } catch (error) {
      throw error; // Pass through the original error
    }
  }

  /**
   * Get all agent versions
   * @returns {Promise<Array<Object>>} List of all agent versions
   * @returns {Promise<Array<AgentVersion>>} List of all agent versions with the following properties for each:
   *   - id {string} - Unique UUID identifier for the agent version
   *   - version {string} - Semantic version string (e.g., '1.2.3')
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
        order: [['created_at', 'DESC']]
      });
      
      return versions;
    } catch (error) {
      throw error; // Pass through the original error
    }
  }
}

module.exports = new AgentService();