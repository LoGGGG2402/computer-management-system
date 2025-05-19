const AdminService = require("../services/admin.service");
const websocketService = require("../services/websocket.service");
const logger = require("../utils/logger");
const fs = require("fs");
const validationUtils = require("../utils/validation");

/**
 * Controller for handling admin-related requests.
 */
class AdminController {
  /**
   * Get system-wide statistics.
   * Requires admin privileges.
   * @param {Object} req - Express request object.
   * @param {Object} res - Express response object.
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - System statistics object (only if status is 'success'):
   *     - totalUsers {number}
   *     - totalRooms {number}
   *     - totalComputers {number}
   *     - onlineComputers {number}
   *     - offlineComputers {number}
   *     - computersWithErrors {number}
   *     - unresolvedErrors {Array<Object>} - List of errors with details
   *   - message {string} - Error message (only if status is 'error')
   */
  async getSystemStats(req, res) {
    try {
      const stats = await AdminService.getSystemStats();

      logger.debug("System statistics retrieved successfully", {
        stats: {
          totalUsers: stats.totalUsers,
          totalComputers: stats.totalComputers,
          onlineComputers: stats.onlineComputers,
          computersWithErrors: stats.computersWithErrors,
        },
        userId: req.user?.id,
      });

      return res.status(200).json({
        status: "success",
        data: stats,
      });
    } catch (error) {
      logger.error("Failed to retrieve system statistics:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      return res.status(500).json({
        status: "error",
        message: error.message || "Failed to retrieve system statistics",
      });
    }
  }

  /**
   * Handle agent package upload
   * @param {Object} req - Express request object
   * @param {Object} req.file - Uploaded file information from multer
   * @param {string} req.file.path - Path where the uploaded file was stored
   * @param {number} req.file.size - Size of the uploaded file in bytes
   * @param {string} req.file.filename - Name of the file on the server's filesystem
   * @param {Object} req.body - Request body
   * @param {string} req.body.version - Semantic version string (e.g. '1.0.0')
   * @param {string} [req.body.notes] - Release notes describing changes and features
   * @param {string} [req.body.checksum] - SHA-256 checksum of the file calculated by the client
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
   *   - data {Object} - Created AgentVersion record (only if status is 'success') with:
   *     - id {string} - Unique UUID identifier for the agent version
   *     - version {string} - Semantic version string (e.g., '1.0.0')
   *     - checksum_sha256 {string} - SHA-256 hash of the agent package for verification
   *     - download_url {string} - URL path where agents can download this version
   *     - notes {string|null} - Release notes for this version
   *     - is_stable {boolean} - Flag indicating if this is a stable production release (initially false)
   *     - file_path {string} - Server filesystem path where the agent package is stored
   *     - file_size {number} - Size of the agent package file in bytes
   *     - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *     - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   */
  async handleAgentUpload(req, res, next) {
    try {
      const errors = [];

      if (!req.file) {
        logger.warn("Agent upload attempt without file", {
          userId: req.user?.id,
          ip: req.ip,
        });

        return res.status(400).json({
          status: "error",
          message: "No agent package file uploaded",
        });
      }

      const maxSize = 50 * 1024 * 1024;
      const regex = /^eag-agent-v\d+\.\d+\.\d+\.(zip|gz|tar|tar\.gz)$/;
      if (!regex.test(req.file.originalname)) {
        errors.push({
          field: "file",
          message: "File must be .zip, .gz, or .tar",
        });
      }
      if (req.file.size > maxSize) {
        errors.push({
          field: "file",
          message: "File size must not exceed 50MB",
        });
      }

      if (!req.body.version) {
        logger.warn("Agent upload attempt without version", {
          userId: req.user?.id,
          ip: req.ip,
          filename: req.file.filename,
        });
        return res.status(400).json({
          status: "error",
          message: "Version is required",
        });
      }
      const versionError = validationUtils.validateSemanticVersion(
        req.body.version
      );
      if (versionError) {
        errors.push({ field: "version", message: versionError });
      }

      if (req.body.notes !== undefined) {
        const notesError = validationUtils.validateAgentVersionNotes(
          req.body.notes
        );
        if (notesError) {
          errors.push({ field: "notes", message: notesError });
        }
      }

      if (req.body.checksum) {
        const checksumError = validationUtils.validateSha256Checksum(
          req.body.checksum
        );
        if (checksumError) {
          errors.push({ field: "checksum", message: checksumError });
        }
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const versionData = {
        version: req.body.version,
        notes: req.body.notes || "",
        client_checksum: req.body.checksum,
      };

      try {
        const createdVersion = await AdminService.processAgentUpload(
          req.file,
          versionData
        );

        logger.info(
          `Agent version ${versionData.version} uploaded successfully`,
          {
            userId: req.user?.id,
            versionId: createdVersion.id,
            checksum: createdVersion.checksum_sha256.substring(0, 8) + "...",
          }
        );

        return res.status(201).json({
          status: "success",
          message: `Agent version ${versionData.version} uploaded successfully`,
          data: createdVersion,
        });
      } catch (uploadError) {
        if (req.file?.path && fs.existsSync(req.file.path)) {
          try {
            fs.unlinkSync(req.file.path);
          } catch (cleanupError) {
            uploadError.cleanupError = cleanupError.message;
          }
        }

        logger.error("Error handling agent upload:", {
          error: uploadError.message,
          stack: uploadError.stack,
          cleanupError: uploadError.cleanupError,
          userId: req.user?.id,
          version: req.body.version,
        });

        next(uploadError);
      }
    } catch (error) {
      logger.error("Error handling agent upload:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      next(error);
    }
  }

  /**
   * Set stability flag for an agent version
   * @param {Object} req - Express request object
   * @param {Object} req.params - URL parameters
   * @param {string} req.params.versionId - Agent version UUID
   * @param {Object} req.body - Request body
   * @param {boolean} req.body.is_stable - Whether to mark as stable
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
   *   - data {Object} - Updated AgentVersion object (only if status is 'success') with:
   *     - id {string} - Unique UUID identifier for the agent version
   *     - version {string} - Semantic version string (e.g., '1.0.0')
   *     - checksum_sha256 {string} - SHA-256 hash of the agent package
   *     - download_url {string} - URL path where agents can download this version
   *     - notes {string|null} - Release notes describing changes and features
   *     - is_stable {boolean} - Flag indicating if this is a stable release (updated value)
   *     - file_path {string} - Server filesystem path where the package is stored
   *     - file_size {number} - Size of the agent package file in bytes
   *     - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *     - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   *   Note: When marked as stable, all other versions are automatically marked as not stable
   */
  async setAgentVersionStability(req, res, next) {
    try {
      const { versionId } = req.params;
      const { is_stable } = req.body;
      const errors = [];

      const versionIdError = validationUtils.validateUuid(versionId);
      if (versionIdError) {
        errors.push({ field: "versionId", message: versionIdError });
      }
      const isStableError = validationUtils.validateIsActiveFlag(is_stable);
      if (isStableError) {
        errors.push({ field: "is_stable", message: isStableError });
      }
      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      if (is_stable === undefined) {
        logger.warn("Stability update attempt without is_stable parameter", {
          userId: req.user?.id,
          versionId,
        });

        return res.status(400).json({
          status: "error",
          message: "is_stable parameter is required",
        });
      }

      try {
        const updatedVersion = await AdminService.updateStabilityFlag(
          versionId,
          is_stable === true
        );

        let logInfo = {
          userId: req.user?.id,
          versionId: updatedVersion.id,
          version: updatedVersion.version,
          isStable: is_stable,
        };

        if (is_stable) {
          websocketService.notifyAgentsOfNewVersion(updatedVersion);
          logInfo.agentsNotified = true;
        }

        logger.info(
          `Agent version ${updatedVersion.version} stability updated to ${
            is_stable ? "stable" : "not stable"
          }`,
          logInfo
        );

        return res.status(200).json({
          status: "success",
          message: `Agent version ${updatedVersion.version} stability updated`,
          data: updatedVersion,
        });
      } catch (stabilityError) {
        logger.error("Error setting agent version stability:", {
          error: stabilityError.message,
          stack: stabilityError.stack,
          userId: req.user?.id,
          versionId,
        });

        next(stabilityError);
      }
    } catch (error) {
      logger.error("Error setting agent version stability:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
        versionId: req.params.versionId,
      });

      next(error);
    }
  }

  /**
   * Get all agent versions
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Array<Object>} - Array of AgentVersion objects, each containing:
   *     - id {string} - Unique UUID identifier for the agent version
   *     - version {string} - Semantic version string (e.g., '1.0.0')
   *     - checksum_sha256 {string} - SHA-256 hash of the agent package
   *     - download_url {string} - URL path where agents can download this version
   *     - notes {string|null} - Release notes describing changes and features
   *     - is_stable {boolean} - Flag indicating if this is a stable production release
   *     - file_path {string} - Server filesystem path where the agent package is stored
   *     - file_size {number} - Size of the agent package file in bytes
   *     - created_at {string} - Timestamp when this version was created (ISO-8601 format)
   *     - updated_at {string} - Timestamp when this version was last modified (ISO-8601 format)
   *   Results are ordered with stable versions first, then by creation date (newest first)
   */
  async getAgentVersions(req, res, next) {
    try {
      const versions = await AdminService.getAllVersions();

      logger.debug(`Retrieved ${versions.length} agent versions`, {
        userId: req.user?.id,
        stableVersions: versions.filter((v) => v.is_stable).length,
      });

      return res.status(200).json({
        status: "success",
        data: versions,
      });
    } catch (error) {
      logger.error("Error getting agent versions:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      next(error);
    }
  }
}

module.exports = new AdminController();
