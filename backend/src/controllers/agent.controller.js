const computerService = require("../services/computer.service");
const mfaService = require("../services/mfa.service");
const websocketService = require("../services/websocket.service");
const roomService = require("../services/room.service");
const agentService = require("../services/agent.service");
const logger = require("../utils/logger");
const path = require("path");
const validationUtils = require("../utils/validation");

/**
 * Controller for agent communication
 */
class AgentController {
  /**
   * Handle agent identification request
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.agentId - Unique identifier for the agent
   * @param {Object} req.body.positionInfo - Information about agent's physical position
   * @param {string} req.body.positionInfo.roomName - Name of the room where the agent is located
   * @param {number} req.body.positionInfo.posX - X position in the room grid
   * @param {number} req.body.positionInfo.posY - Y position in the room grid
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with one of the following formats:
   *   - If agent exists with token already assigned:
   *     - status {string} - 'success'
   *   - If position is valid and MFA is required:
   *     - status {string} - 'mfa_required'
   *   - If position is invalid:
   *     - status {string} - 'position_error'
   *     - message {string} - Detailed error message about position
   *   - If error occurs:
   *     - status {string} - 'error'
   *     - message {string} - Error message
   */
  async handleIdentifyRequest(req, res, next) {
    const { agentId, positionInfo } = req.body;
    const errors = [];

    if (!agentId) {
      logger.warn("Agent identification attempt without agent ID", {
        ip: req.ip,
      });
      return res.status(400).json({
        status: "error",
        message: "Missing required fields",
      });
    }

    const agentIdError = validationUtils.validateAgentId(agentId);
    if (agentIdError) {
      errors.push({ field: "agentId", message: agentIdError });
    }

    try {
      const computer = await computerService.findComputerByAgentId(agentId);
      if (computer?.agent_token_hash) {
        logger.debug(
          `Agent already registered: ${agentId}, Computer ID: ${computer.id}`
        );
        return res.status(200).json({
          status: "success",
          agentToken: computer.agent_token,
        });
      }

      const isValidPosition =
        positionInfo &&
        positionInfo.roomName &&
        positionInfo.posX !== undefined &&
        positionInfo.posY !== undefined;

      if (!isValidPosition) {
        logger.warn("Agent identification attempt with invalid position info", {
          ip: req.ip,
          agentId,
          positionInfo,
        });
        return res.status(400).json({
          status: "position_error",
          message: "Valid position information is required",
        });
      }

      const roomNameError = validationUtils.validateRoomName(
        positionInfo.roomName
      );
      if (roomNameError) {
        errors.push({ field: "positionInfo.roomName", message: roomNameError });
      }

      const posXError = validationUtils.validatePositionCoordinate(
        positionInfo.posX,
        "posX"
      );
      if (posXError) {
        errors.push({ field: "positionInfo.posX", message: posXError });
      }

      const posYError = validationUtils.validatePositionCoordinate(
        positionInfo.posY,
        "posY"
      );
      if (posYError) {
        errors.push({ field: "positionInfo.posY", message: posYError });
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "position_error",
          message: "Validation failed",
          errors,
        });
      }

      const { roomName, posX, posY } = positionInfo;

      const result = await roomService.isPositionAvailable(
        roomName,
        posX,
        posY
      );

      if (!result.valid) {
        logger.warn(
          `Invalid position for agent: ${agentId}, Room: ${roomName} (${posX},${posY})`,
          {
            reason: result.message,
          }
        );
        return res.status(400).json({
          status: "position_error",
          message: result.message,
        });
      }

      const mfaCode = mfaService.generateAndStoreMfa(agentId, {
        roomId: result.room.id,
        posX,
        posY,
      });

      websocketService.notifyAdminsNewMfa(mfaCode, positionInfo);

      logger.info(
        `MFA required for new agent: ${agentId}, Room: ${roomName} (${posX},${posY})`
      );
      return res.status(200).json({ status: "mfa_required" });
    } catch (error) {
      if (positionInfo) {
        const { roomName, posX, posY } = positionInfo;
        logger.error(`Error checking position for agent ${agentId}:`, {
          error: error.message,
          stack: error.stack,
          room: roomName,
          position: `(${posX},${posY})`,
        });
      } else {
        logger.error(`Error in agent identification request:`, {
          error: error.message,
          stack: error.stack,
          agentId,
        });
      }
      next(error);
    }
  }

  /**
   * Handle agent MFA verification
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.agentId - Unique identifier for the agent
   * @param {string} req.body.mfaCode - MFA code to verify
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with one of the following formats:
   *   - If MFA verification is successful:
   *     - status {string} - 'success'
   *     - agentToken {string} - Plain text token for agent authentication
   *   - If MFA verification fails:
   *     - status {string} - 'error'
   *     - message {string} - Error message
   */
  async handleVerifyMfa(req, res, next) {
    try {
      const { agentId, mfaCode } = req.body;
      const errors = [];

      if (!agentId || !mfaCode) {
        logger.warn("MFA verification attempt with missing data", {
          hasAgentId: !!agentId,
          hasMfaCode: !!mfaCode,
          ip: req.ip,
        });
        return res.status(400).json({
          status: "error",
          message: "Missing required fields",
        });
      }

      const agentIdError = validationUtils.validateAgentId(agentId);
      if (agentIdError) {
        errors.push({ field: "agentId", message: agentIdError });
      }

      const mfaCodeError = validationUtils.validateMfaCode(mfaCode);
      if (mfaCodeError) {
        errors.push({ field: "mfaCode", message: mfaCodeError });
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: "Validation failed",
          errors,
        });
      }

      const existingComputer = await computerService.findComputerByAgentId(
        agentId
      );
      if (!existingComputer && !mfaService.hasPendingMfa(agentId)) {
        return res.status(404).json({
          status: "error",
          message: "Agent ID not found",
        });
      }

      const { valid, positionInfo } = mfaService.verifyMfa(agentId, mfaCode);

      if (valid) {
        try {
          const { computer, plainToken } =
            await computerService.generateAndAssignAgentToken(
              agentId,
              positionInfo
            );
          websocketService.notifyAdminsAgentRegistered(
            computer.id,
            positionInfo
          );
          logger.info(
            `Agent ${agentId} registered successfully with MFA, Computer ID: ${computer.id}`
          );
          return res.status(200).json({
            status: "success",
            agentToken: plainToken,
          });
        } catch (tokenError) {
          logger.error(
            `Failed to generate token after MFA verification for agent ${agentId}:`,
            {
              error: tokenError.message,
              stack: tokenError.stack,
              position: positionInfo,
            }
          );
          return res.status(500).json({
            status: "error",
            message: "Failed to verify MFA code",
          });
        }
      } else {
        logger.warn(`Invalid MFA verification attempt for agent: ${agentId}`);
        return res.status(401).json({
          status: "error",
          message: "Invalid or expired MFA code",
        });
      }
    } catch (error) {
      logger.error(`Error in MFA verification:`, {
        error: error.message,
        stack: error.stack,
        agentId: req.body.agentId,
      });
      next(error);
    }
  }

  /**
   * Handle hardware information update from agent
   * @param {Object} req - Express request object
   * @param {Object} req.computer - Computer object from authentication middleware
   * @param {number} req.computer.id - Computer ID in the database
   * @param {Object} req.body - Request body with hardware information
   * @param {number} req.body.total_disk_space - Total disk space in MB (required)
   * @param {string} [req.body.gpu_info] - Information about GPU (0-500 characters)
   * @param {string} [req.body.cpu_info] - Information about CPU (0-500 characters)
   * @param {number} [req.body.total_ram] - Total RAM in MB
   * @param {string} [req.body.os_info] - Information about operating system (0-200 characters)
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} Empty response with 204 status code on success,
   *                  or error response with message on failure
   */
  async handleHardwareInfo(req, res, next) {
    const computerId = req.computerId;
    const agentId = req.agentId;
    const { total_disk_space, gpu_info, cpu_info, total_ram, os_info } =
      req.body;
    const errors = [];

    if (!total_disk_space) {
      logger.warn(
        `Hardware info update missing total_disk_space for computer ${computerId}`
      );
      return res.status(400).json({
        status: "error",
        message: "Total disk space is required",
      });
    }

    const diskSpaceError = validationUtils.validatePositiveInteger(
      total_disk_space,
      "Total Disk Space"
    );
    if (diskSpaceError) {
      errors.push({ field: "total_disk_space", message: diskSpaceError });
    }

    if (total_ram !== undefined) {
      const totalRamError = validationUtils.validatePositiveInteger(
        total_ram,
        "Total RAM"
      );
      if (totalRamError) {
        errors.push({ field: "total_ram", message: totalRamError });
      }
    }

    if (gpu_info !== undefined) {
      const gpuInfoError = validationUtils.validateMaxLength(
        gpu_info,
        validationUtils.constants.GPU_INFO_MAX_LENGTH,
        "GPU Info"
      );
      if (gpuInfoError) {
        errors.push({ field: "gpu_info", message: gpuInfoError });
      }
    }

    if (cpu_info !== undefined) {
      const cpuInfoError = validationUtils.validateMaxLength(
        cpu_info,
        validationUtils.constants.CPU_INFO_MAX_LENGTH,
        "CPU Info"
      );
      if (cpuInfoError) {
        errors.push({ field: "cpu_info", message: cpuInfoError });
      }
    }

    if (os_info !== undefined) {
      const osInfoError = validationUtils.validateMaxLength(
        os_info,
        validationUtils.constants.OS_INFO_MAX_LENGTH,
        "OS Info"
      );
      if (osInfoError) {
        errors.push({ field: "os_info", message: osInfoError });
      }
    }

    if (errors.length > 0) {
      return res.status(400).json({
        status: "error",
        message: "Validation failed",
        errors,
      });
    }

    try {
      const hardwareData = {
        os_info: os_info || null,
        total_disk_space: total_disk_space || null,
        gpu_info: gpu_info || null,
        cpu_info: cpu_info || null,
        total_ram: total_ram || null,
      };

      await computerService.updateComputer(computerId, hardwareData);

      logger.info(`Hardware info updated for computer ${computerId}`, {
        agentId,
        os: os_info,
        cpu: cpu_info,
        ram: total_ram,
      });

      return res.sendStatus(204);
    } catch (error) {
      logger.error(
        `Failed to update hardware info for computer ${computerId}:`,
        {
          error: error.message,
          stack: error.stack,
          agentId,
          computerId,
        }
      );

      next(error);
    }
  }

  /**
   * Handle error report from agent
   * @param {Object} req - Express request object
   * @param {string} req.agentId - Agent ID (attached by authAgentToken middleware)
   * @param {number} req.computerId - Computer ID (attached by authAgentToken middleware)
   * @param {Object} req.body - Request body with error details
   * @param {string} req.body.type - Type of error (2-50 characters)
   * @param {string} req.body.message - Error message (5-255 characters)
   * @param {Object} req.body.details - Additional error details (optional, max 2KB)
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} Empty response with 204 status code on success
   */
  async handleErrorReport(req, res, next) {
    const computerId = req.computerId;
    const agentId = req.agentId;
    const { type, message, details } = req.body;
    const errors = [];

    if (!type || !message) {
      logger.warn(
        `Error report missing required fields from agent: ${agentId}, Computer: ${computerId}`
      );
      return res.status(400).json({
        status: "error",
        message: "Error type and message are required",
      });
    }

    const errorTypeError = validationUtils.validateAgentErrorType(type);
    if (errorTypeError) {
      errors.push({ field: "type", message: errorTypeError });
    }

    if (type.startsWith("Update")) {
      if (!validationUtils.constants.AGENT_UPDATE_ERROR_TYPES.includes(type)) {
        errors.push({
          field: "type",
          message: `Invalid error type for update. Must be one of: ${validationUtils.constants.AGENT_UPDATE_ERROR_TYPES.join(
            ", "
          )}`,
        });
      }
    }

    const errorMessageError =
      validationUtils.validateAgentErrorMessage(message);
    if (errorMessageError) {
      errors.push({ field: "message", message: errorMessageError });
    }

    if (details !== undefined) {
      const detailsError = validationUtils.validateErrorDetailsSize(details);
      if (detailsError) {
        errors.push({ field: "details", message: detailsError });
      }
    }

    if (errors.length > 0) {
      return res.status(400).json({
        status: "error",
        message: "Validation failed",
        errors,
      });
    }

    const errorData = {
      error_type: type,
      error_message: message,
      error_details: details || {},
    };

    try {
      const result = await computerService.reportComputerError(
        computerId,
        errorData
      );

      logger.info(`Error reported for computer ${computerId}: ${type}`, {
        errorId: result.error.id,
        agentId,
      });

      return res.status(204).end();
    } catch (error) {
      logger.error(`Failed to save error report for computer ${computerId}:`, {
        error: error.message,
        stack: error.stack,
        agentId,
        errorType: type,
      });

      next(error);
    }
  }

  /**
   * Handle agent check for update request
   * @param {Object} req - Express request object
   * @param {string} req.agentId - Agent ID (attached by authAgentToken middleware)
   * @param {number} req.computerId - Computer ID (attached by authAgentToken middleware)
   * @param {string} req.query.current_version - The agent's current version
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} JSON response with update info if available, otherwise 204 No Content
   * @returns {Object} If update available, JSON response with the following AgentVersion properties:
   *   - status {string} - Always 'success' when an update is available
   *   - update_available {boolean} - Always true when an update is available
   *   - version {string} - Semantic version string (e.g., '1.2.3') of the new version
   *   - download_url {string} - URL path where the agent can download the new version package
   *   - checksum_sha256 {string} - SHA-256 hash of the agent package for integrity verification
   *   - notes {string|null} - Release notes describing changes and features in this version
   */
  async handleCheckUpdate(req, res, next) {
    const { current_version } = req.query;
    const agentId = req.agentId;
    const computerId = req.computerId;
    const errors = [];

    if (!current_version) {
      logger.warn(
        `Update check missing current_version from agent: ${agentId}, Computer: ${computerId}`
      );
      return res.status(400).json({
        status: "error",
        message: "Current version parameter is required",
      });
    }

    const versionError =
      validationUtils.validateSemanticVersion(current_version);
    if (versionError) {
      errors.push({ field: "current_version", message: versionError });
    }

    if (errors.length > 0) {
      return res.status(400).json({
        status: "error",
        message: "Validation failed",
        errors,
      });
    }

    try {
      const updateInfo = await agentService.getLatestStableVersionInfo(
        current_version
      );

      if (!updateInfo) {
        logger.debug(
          `No update available for agent: ${agentId}, Current version: ${current_version}`
        );
        return res.status(204).end();
      }

      logger.info(
        `Update available for agent: ${agentId}, Current: ${current_version}, Latest: ${updateInfo.version}`
      );
      return res.status(200).json({
        status: "success",
        update_available: true,
        version: updateInfo.version,
        download_url: updateInfo.download_url,
        checksum_sha256: updateInfo.checksum_sha256,
        notes: updateInfo.notes || "",
      });
    } catch (error) {
      logger.error(`Failed to check for agent updates:`, {
        error: error.message,
        stack: error.stack,
        agentId,
        computerId,
        currentVersion: current_version,
      });

      return res.status(500).json({
        status: "error",
        message: "Failed to check for agent updates",
      });
    }
  }

  /**
   * Handle secure agent package downloads
   * @param {Object} req - Express request object
   * @param {string} req.agentId - Agent ID (attached by authAgentToken middleware)
   * @param {number} req.computerId - Computer ID (attached by authAgentToken middleware)
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.filename - Filename of the agent package to download
   * @param {Object} res - Express response object
   * @returns {Object} File stream or error response
   */
  handleAgentPackageDownload(req, res) {
    const agentId = req.agentId;
    const computerId = req.computerId;
    const { filename } = req.params;
    const errors = [];

    if (!filename) {
      logger.warn(`Download attempt without filename by agent ${agentId}`);
      return res.status(400).json({
        status: "error",
        message: "Invalid filename format",
      });
    }

    const filenameError = validationUtils.validateFilename(filename);
    if (filenameError) {
      errors.push({ field: "filename", message: filenameError });
    }

    if (errors.length > 0) {
      return res.status(400).json({
        status: "error",
        message: "Validation failed",
        errors,
      });
    }

    try {
      const AGENT_PACKAGES_DIR = path.join(
        __dirname,
        "../../uploads/agent-packages"
      );
      const filePath = path.join(AGENT_PACKAGES_DIR, filename);

      logger.info(`Agent ${agentId} downloading package: ${filename}`, {
        computerId,
      });

      res.sendFile(filePath, (err) => {
        if (!err) return;

        const errorDetails = {
          error: err.message,
          computerId,
          agentId,
        };

        logger.error(`Error serving agent package ${filename}:`, errorDetails);

        if (err.code === "ENOENT") {
          return res.status(404).json({
            status: "error",
            message: "File not found",
          });
        }

        return res.status(500).json({
          status: "error",
          message: "Error serving file",
        });
      });
    } catch (error) {
      logger.error(`Error serving agent package:`, {
        error: error.message,
        stack: error.stack,
        agentId,
        filename,
      });

      return res.status(500).json({
        status: "error",
        message: "Error serving file",
      });
    }
  }
}

module.exports = new AgentController();
