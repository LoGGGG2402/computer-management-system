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
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleIdentifyRequest(req, res, next) {
    try {
      const { unique_agent_id, positionInfo, forceRenewToken } = req.body;

      console.log("[AgentController] Received identify request:", {
        unique_agent_id,
        positionInfo,
        forceRenewToken,
      });

      if (!unique_agent_id) {
        return res.status(400).json({
          status: "error",
          message: "Agent ID is required",
        });
      }

      // Check if computer with this agent ID exists and has a token
      const computer = await computerService.findComputerByAgentId(
        unique_agent_id
      );
      console.log("[AgentController] Found computer:", computer);

      // Kiểm tra nếu client yêu cầu cấp mới token và computer đã tồn tại trong database
      if (forceRenewToken && computer) {
        if (computer.room.name === positionInfo.roomName,
          computer.pos_x === positionInfo.posX,
          computer.pos_y === positionInfo.posY)
          {

        console.log("[AgentController] Client requested token renewal");
        // Generate and assign a new token for the agent
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
        console.log("[AgentController] Agent exists, token already assigned");
        return res.status(200).json({
          status: "success",
        });
      }

      // Check if position is available
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
          unique_agent_id,
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
      console.error("[AgentController] Error in handleIdentifyRequest:", error);
      return res.status(500).json({
        status: "error",
        message: "Internal server error",
      });
    }
  }

  /**
   * Handle agent MFA verification
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleVerifyMfa(req, res, next) {
    try {
      const { unique_agent_id, mfaCode } = req.body;

      console.log("[AgentController] Received verify MFA request:", {
        unique_agent_id,
        positionInfo,
      });

      if (!unique_agent_id || !mfaCode) {
        return res.status(400).json({
          status: "error",
          message: "Agent ID and MFA code are required",
        });
      }

      // Verify the MFA code with positionInfo for additional security
      const {isValid, positionInfo} = mfaService.verifyMfa(
        unique_agent_id,
        mfaCode
      );

      if (isValid) {
        // Generate and assign a token for the agent, cùng với thông tin vị trí
        const { computer, plainToken } =
          await computerService.generateAndAssignAgentToken(
            unique_agent_id,
            positionInfo
          );

        // Notify admins about successful registration
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
      console.error("[AgentController] Error in handleVerifyMfa:", error);
      next(error);
    }
  }

  /**
   * Handle hardware information update from agent
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleHardwareInfo(req, res, next) {
    try {
      // Get the computer ID from the authenticated request
      const computerId = req.computer.id;

      // Get hardware information from request body
      const { total_disk_space, gpu_info, cpu_info, total_ram, os_info } = req.body;

      console.log(
        `[AgentController] Received hardware info from computer ${computerId}:`,
        { total_disk_space, gpu_info, cpu_info, total_ram }
      );

      if (!total_disk_space) {
        return res.status(400).json({
          status: "error",
          message: "Total disk space is required",
        });
      }

      // Update the computer record with new hardware information
      await computerService.updateComputer(computerId, {
        os_info: os_info || null,
        total_disk_space: total_disk_space || null,
        gpu_info: gpu_info || null,
        cpu_info: cpu_info || null,
        total_ram: total_ram || null,
      });

      // Return 204 No Content status
      return res.sendStatus(204);
    } catch (error) {
      console.error(`[AgentController] Error handling hardware info:`, error);
      next(error);
    }
  }
}

module.exports = new AgentController();
