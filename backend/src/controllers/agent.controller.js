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
      if (
        (forceRenewToken && computer && computer.room_id === positionInfo.roomId,
        computer.pos_x === positionInfo.posX,
        computer.pos_y === positionInfo.posY)
      ) {
        console.log("[AgentController] Client requested token renewal");
        // Generate and assign a new token for the agent
        const { plainToken } =
          await computerService.generateAndAssignAgentToken(
            unique_agent_id,
            null,
            computer
          );

        // Notify admins about token renewal
        websocketService.notifyAdminsAgentRegistered(
          unique_agent_id,
          computer.id
        );

        return res.status(200).json({
          status: "success",
          agentToken: plainToken,
        });
      }

      if (computer && computer.agent_token_hash) {
        console.log("[AgentController] Agent exists, token already assigned");
        return res.status(200).json({
          status: "success",
        });
      }

      // Check if position is available
      let resulst = await roomService.isPositionAvailable(
        positionInfo.room,
        positionInfo.posX,
        positionInfo.posY
      );
      if (resulst.valid) {
        let mfacode = mfaService.generateAndStoreMfa(unique_agent_id, positionInfo);
        console.log("[AgentController] Generated MFA code:", {
          unique_agent_id,
          mfacode,
          positionInfo,
        });
        // Notify admins about the new MFA request with room info
        websocketService.notifyAdminsNewMfa(unique_agent_id, mfacode, positionInfo);
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
      const { unique_agent_id, mfaCode, positionInfo } = req.body;

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
      const isValid = mfaService.verifyMfa(unique_agent_id, mfaCode, positionInfo);

      if (isValid) {
        // Generate and assign a token for the agent, cùng với thông tin vị trí
        const { computer, plainToken } =
          await computerService.generateAndAssignAgentToken(
            unique_agent_id,
            positionInfo
          );

        // Notify admins about successful registration
        websocketService.notifyAdminsAgentRegistered(
          unique_agent_id,
          computer.id
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
   * Handle agent status update - DEPRECATED: Now handled via WebSocket
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   * @deprecated Use WebSocket communication instead
   */
  async handleStatusUpdate(req, res, next) {
    console.warn("[AgentController] Deprecated HTTP route called: handleStatusUpdate");
    try {
      // Get the computer ID from the authenticated request
      const computerId = req.computer.id;

      // Get system metrics from request body
      const { cpu, ram, disk } = req.body;

      console.log(
        `[AgentController] Received status update from computer ${computerId}:`,
        { cpu, ram, disk }
      );

      // Update the realtime cache with new system metrics
      websocketService.updateRealtimeCache(computerId, {
        cpuUsage: cpu,
        ramUsage: ram,
        diskUsage: disk
      });

      // Update the computer's last seen timestamp in the database
      await computerService.updateLastSeen(computerId);

      // Broadcast the status update to clients
      await websocketService.broadcastStatusUpdate(computerId);

      // Return 204 No Content status
      return res.sendStatus(204);
    } catch (error) {
      console.error(`[AgentController] Error handling status update:`, error);
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
      const { total_disk_space, gpu_info, cpu_info, total_ram } = req.body;

      console.log(
        `[AgentController] Received hardware info from computer ${computerId}:`,
        { total_disk_space, gpu_info, cpu_info, total_ram }
      );

      if (!total_disk_space) {
        return res.status(400).json({
          status: "error",
          message: "Total disk space is required"
        });
      }

      // Update the computer record with new hardware information
      await computerService.updateComputer(computerId, {
        total_disk_space,
        gpu_info: gpu_info || null,
        cpu_info: cpu_info || null,
        total_ram: total_ram || null
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
