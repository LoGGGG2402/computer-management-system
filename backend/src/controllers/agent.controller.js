const computerService = require("../services/computer.service");
const mfaService = require("../services/mfa.service");
const websocketService = require("../services/websocket.service");
const roomService = require("../services/room.service");

/**
 * Controller for agent communication
 */
class AgentController {
  /**
   * Handle agent identification request
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.unique_agent_id - Unique identifier for the agent
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
    try {
      const { unique_agent_id, positionInfo, forceRenewToken } = req.body;

      if (!unique_agent_id) {
        return res.status(400).json({
          status: "error",
          message: "Agent ID is required",
        });
      }

      const computer = await computerService.findComputerByAgentId(
        unique_agent_id
      );

      if (forceRenewToken && computer) {
        if (computer.room.name === positionInfo.roomName,
          computer.pos_x === positionInfo.posX,
          computer.pos_y === positionInfo.posY)
          {
        const { plainToken } =
          await computerService.generateAndAssignAgentToken(
            unique_agent_id,
            null,
            computer
          );

        return res.status(200).json({
          status: "success",
          agentToken: plainToken,
        });
          }
      }

      if (computer && computer.agent_token_hash) {
        return res.status(200).json({
          status: "success",
        });
      }

      let resulst = await roomService.isPositionAvailable(
        positionInfo.roomName,
        positionInfo.posX,
        positionInfo.posY
      );
      if (resulst.valid) {
        let mfacode = mfaService.generateAndStoreMfa(
          unique_agent_id,
          {
            roomId: resulst.room.id,
            posX: positionInfo.posX,
            posY: positionInfo.posY,
          }
        );
        websocketService.notifyAdminsNewMfa(
          mfacode,
          positionInfo
        );
        return res.status(200).json({
          status: "mfa_required",
        });
      } else {
        return res.status(400).json({
          status: "position_error",
          message: resulst.message,
        });
      }
    } catch (error) {
      return res.status(500).json({
        status: "error",
        message: "Internal server error",
      });
    }
  }

  /**
   * Handle agent MFA verification
   * @param {Object} req - Express request object
   * @param {Object} req.body - Request body
   * @param {string} req.body.unique_agent_id - Unique identifier for the agent
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
      const { unique_agent_id, mfaCode } = req.body;

      if (!unique_agent_id || !mfaCode) {
        return res.status(400).json({
          status: "error",
          message: "Agent ID and MFA code are required",
        });
      }

      const { valid, positionInfo } = mfaService.verifyMfa(
        unique_agent_id,
        mfaCode
      );

      if (valid) {
        const { computer, plainToken } =
          await computerService.generateAndAssignAgentToken(
            unique_agent_id,
            positionInfo
          );

        websocketService.notifyAdminsAgentRegistered(
          computer.id,
          positionInfo
        );

        return res.status(200).json({
          status: "success",
          agentToken: plainToken,
        });
      } else {
        return res.status(401).json({
          status: "error",
          message: "Invalid or expired MFA code",
        });
      }
    } catch (error) {
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
      const computerId = req.computer.id;

      const { total_disk_space, gpu_info, cpu_info, total_ram, os_info } = req.body;

      if (!total_disk_space) {
        return res.status(400).json({
          status: "error",
          message: "Total disk space is required",
        });
      }

      await computerService.updateComputer(computerId, {
        os_info: os_info || null,
        total_disk_space: total_disk_space || null,
        gpu_info: gpu_info || null,
        cpu_info: cpu_info || null,
        total_ram: total_ram || null,
      });

      return res.sendStatus(204);
    } catch (error) {
      next(error);
    }
  }
}

module.exports = new AgentController();
