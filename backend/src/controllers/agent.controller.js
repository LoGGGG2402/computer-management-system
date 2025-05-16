const computerService = require("../services/computer.service");
const mfaService = require("../services/mfa.service");
const websocketService = require("../services/websocket.service");
const roomService = require("../services/room.service");
const agentService = require("../services/agent.service");
const logger = require('../utils/logger');
const path = require('path');

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
   * @param {boolean} [req.body.forceRenewToken] - Whether to force token renewal
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
   *   - If token renewal is requested:
   *     - status {string} - 'success'
   *     - agentToken {string} - New plain text token for agent authentication
   *   - If error occurs:
   *     - status {string} - 'error'
   *     - message {string} - Error message
   */
  async handleIdentifyRequest(req, res, next) {
    const { agentId, positionInfo, forceRenewToken } = req.body;

    try {
      if (!agentId) {
        logger.warn('Agent identification attempt without agent ID', { ip: req.ip });
        return res.status(400).json({
          status: "error",
          message: "Agent ID is required"
        });
      }

      const computer = await computerService.findComputerByAgentId(agentId);

      if (forceRenewToken && computer) {
        if (computer.room.name === positionInfo.roomName &&
          computer.pos_x === positionInfo.posX &&
          computer.pos_y === positionInfo.posY) {
          try {
            const { plainToken } = await computerService.generateAndAssignAgentToken(
              agentId,
              null,
              computer
            );
            logger.info(`Token renewed for agent: ${agentId}, Computer ID: ${computer.id}`);
            return res.status(200).json({
              status: "success",
              agentToken: plainToken,
            });
          } catch (tokenError) {
            logger.error(`Failed to renew token for agent ${agentId}:`, {
              error: tokenError.message,
              stack: tokenError.stack,
              computerId: computer.id
            });
            next(tokenError);
            return;
          }
        }
      }

      if (computer && computer.agent_token_hash) {
        logger.debug(`Agent already registered: ${agentId}, Computer ID: ${computer.id}`);
        return res.status(200).json({
          status: "success",
        });
      }

      try {
        const result = await roomService.isPositionAvailable(
          positionInfo.roomName,
          positionInfo.posX,
          positionInfo.posY
        );

        if (result.valid) {
          const mfaCode = mfaService.generateAndStoreMfa(
            agentId,
            {
              roomId: result.room.id,
              posX: positionInfo.posX,
              posY: positionInfo.posY,
            }
          );
          websocketService.notifyAdminsNewMfa(
            mfaCode,
            positionInfo
          );
          logger.info(`MFA required for new agent: ${agentId}, Room: ${positionInfo.roomName} (${positionInfo.posX},${positionInfo.posY})`);
          return res.status(200).json({
            status: "mfa_required",
          });
        } else {
          logger.warn(`Invalid position for agent: ${agentId}, Room: ${positionInfo.roomName} (${positionInfo.posX},${positionInfo.posY})`, {
            reason: result.message
          });
          return res.status(400).json({
            status: "position_error",
            message: result.message,
          });
        }
      } catch (positionError) {
        logger.error(`Error checking position for agent ${agentId}:`, {
          error: positionError.message,
          stack: positionError.stack,
          room: positionInfo.roomName,
          position: `(${positionInfo.posX},${positionInfo.posY})`
        });
        next(positionError);
        return;
      }
    } catch (error) {
      logger.error(`Error in agent identification request:`, {
        error: error.message,
        stack: error.stack,
        agentId: agentId
      });
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

      if (!agentId || !mfaCode) {
        logger.warn('MFA verification attempt with missing data', {
          hasAgentId: !!agentId,
          hasMfaCode: !!mfaCode,
          ip: req.ip
        });
        return res.status(400).json({
          status: "error",
          message: "Agent ID and MFA code are required",
        });
      }

      const { valid, positionInfo } = mfaService.verifyMfa(
        agentId,
        mfaCode
      );

      if (valid) {
        try {
          const { computer, plainToken } = await computerService.generateAndAssignAgentToken(
            agentId,
            positionInfo
          );
          websocketService.notifyAdminsAgentRegistered(
            computer.id,
            positionInfo
          );
          logger.info(`Agent ${agentId} registered successfully with MFA, Computer ID: ${computer.id}`);
          return res.status(200).json({
            status: "success",
            agentToken: plainToken,
          });
        } catch (tokenError) {
          logger.error(`Failed to generate token after MFA verification for agent ${agentId}:`, {
            error: tokenError.message,
            stack: tokenError.stack,
            position: positionInfo
          });
          next(tokenError);
          return;
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
        agentId: req.body.agentId
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
   * @param {number} [req.body.total_disk_space] - Total disk space in GB
   * @param {Object} [req.body.gpu_info] - Information about GPU
   * @param {Object} [req.body.cpu_info] - Information about CPU
   * @param {number} [req.body.total_ram] - Total RAM in GB
   * @param {Object} [req.body.os_info] - Information about operating system
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} Empty response with 204 status code on success,
   *                  or error response with message on failure
   */
  async handleHardwareInfo(req, res, next) {
    try {
      const computerId = req.computerId;
      const agentId = req.agentId;
      const { total_disk_space, gpu_info, cpu_info, total_ram, os_info } = req.body;

      if (!total_disk_space) {
        logger.warn(`Hardware info update missing total_disk_space for computer ${computerId}`);
        return res.status(400).json({
          status: "error",
          message: "Total disk space is required",
        });
      }

      try {
        await computerService.updateComputer(computerId, {
          os_info: os_info || null,
          total_disk_space: total_disk_space || null,
          gpu_info: gpu_info || null,
          cpu_info: cpu_info || null,
          total_ram: total_ram || null,
        });
        logger.info(`Hardware info updated for computer ${computerId}`, {
          agentId,
          os: os_info?.name,
          cpu: cpu_info?.model,
          ram: total_ram
        });
        return res.sendStatus(204);
      } catch (updateError) {
        logger.error(`Failed to update hardware info for computer ${computerId}:`, {
          error: updateError.message,
          stack: updateError.stack,
          agentId
        });
        next(updateError);
        return;
      }
    } catch (error) {
      logger.error(`Error handling hardware info update:`, {
        error: error.message,
        stack: error.stack,
        computerId: req.computerId,
        agentId: req.agentId
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
    try {
      const { current_version } = req.query;

      try {
        const updateInfo = await agentService.getLatestStableVersionInfo(current_version);

        if (!updateInfo) {
          logger.debug(`No update available for agent: ${req.agentId}, Current version: ${current_version || 'unknown'}`);
          return res.status(204).end(); // 204 No Content
        }
        logger.info(`Update available for agent: ${req.agentId}, Current: ${current_version || 'unknown'}, Latest: ${updateInfo.version}`);
        return res.status(200).json({
          status: "success",
          update_available: true,
          version: updateInfo.version,
          download_url: updateInfo.download_url,
          checksum_sha256: updateInfo.checksum_sha256,
          notes: updateInfo.notes
        });
        
      } catch (versionError) {
        logger.error(`Failed to check for agent updates:`, {
          error: versionError.message,
          stack: versionError.stack,
          agentId: req.agentId,
          computerId: req.computerId,
          currentVersion: current_version
        });
        console.error(`Error checking for updates for agent ${req.agentId}:`, versionError);
        next(versionError);
        return;
      }
    } catch (error) {
      logger.error(`Error handling check update request from agent ${req.agentId}:`, {
        error: error.message,
        stack: error.stack
      });
      next(error);
    }
  }

  /**
   * Handle error report from agent
   * @param {Object} req - Express request object
   * @param {string} req.agentId - Agent ID (attached by authAgentToken middleware)
   * @param {number} req.computerId - Computer ID (attached by authAgentToken middleware)
   * @param {Object} req.body - Request body with error details
   * @param {string} req.body.error_type - Type of error
   * @param {string} req.body.error_message - Error message
   * @param {Object} req.body.error_details - Additional error details including stack trace and agent version
   * @param {string} [req.body.timestamp] - When the report was sent (used internally)
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @returns {Object} Empty response with 204 status code on success
   */
  async handleErrorReport(req, res, next) {
    try {
      const computerId = req.computerId;
      const { error_type, error_message, error_details } = req.body;

      if (!error_message) {
        logger.warn(`Error report missing required fields from agent: ${req.agentId}, Computer: ${computerId}`);
        return res.status(400).json({
          status: "error",
          message: "Error type and message are required"
        });
      }

      // Extract data directly from the received format
      const errorData = {
        error_type: error_type || 'unknown',
        error_message,
        error_details: error_details || {}
      };

      try {
        const result = await computerService.reportComputerError(computerId, errorData);
        logger.info(`Error reported for computer ${computerId}: ${error_type}`, {
          errorId: result.error.id,
          agentId: req.agentId
        });
        return res.status(204).end();
      } catch (reportError) {
        logger.error(`Failed to save error report for computer ${computerId}:`, {
          error: reportError.message,
          stack: reportError.stack,
          agentId: req.agentId,
          errorType: error_type
        });
        next(reportError);
        return;
      }
    } catch (error) {
      logger.error(`Error handling error report request from agent ${req.agentId}:`, {
        error: error.message,
        stack: error.stack
      });
      next(error);
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
    try {
      const filename = req.params.filename;
      if (!filename) {
        logger.warn(`Download attempt without filename by agent ${req.agentId}`);
        return res.status(404).json({ status: 'error', message: 'File not found' });
      }

      const AGENT_PACKAGES_DIR = path.join(__dirname, '../../uploads/agent-packages');
      const filePath = path.join(AGENT_PACKAGES_DIR, filename);

      logger.info(`Agent ${req.agentId} downloading package: ${filename}`, {
        computerId: req.computerId
      });

      // Send the file securely (only to authenticated agents)
      res.sendFile(filePath, (err) => {
        if (err) {
          logger.error(`Error serving agent package ${filename}:`, {
            error: err.message,
            computerId: req.computerId,
            agentId: req.agentId
          });
          if (err.code === 'ENOENT') {
            return res.status(404).json({ status: 'error', message: 'File not found' });
          }
          return res.status(500).json({ status: 'error', message: 'Error serving file' });
        }
      });
    } catch (error) {
      logger.error(`Error serving agent package:`, {
        error: error.message,
        stack: error.stack,
        agentId: req.agentId
      });
      return res.status(500).json({
        status: 'error',
        message: 'Error serving file'
      });
    }
  }
}

module.exports = new AgentController();
