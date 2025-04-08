const computerService = require('../services/computer.service');
const mfaService = require('../services/mfa.service');
const websocketService = require('../services/websocket.service');
const roomService = require('../services/room.service');

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
      const { unique_agent_id, roomInfo, forceRenewToken } = req.body;
      
      console.log('[AgentController] Received identify request:', { unique_agent_id, roomInfo, forceRenewToken });
      
      if (!unique_agent_id) {
        return res.status(400).json({
          status: 'error',
          message: 'Agent ID is required'
        });
      }
      
      // Check if computer with this agent ID exists and has a token
      const computer = await computerService.findComputerByAgentId(unique_agent_id);
      console.log('[AgentController] Found computer:', computer);
      
      // Kiểm tra nếu client yêu cầu cấp mới token và computer đã tồn tại trong database
      if (forceRenewToken && computer) {
        console.log('[AgentController] Client requested token renewal');
        
        // Kiểm tra vị trí và room
        let roomId = computer.room_id;
        let posX = computer.pos_x;
        let posY = computer.pos_y;
        
        // Sử dụng vị trí từ request nếu có và hợp lệ
        if (roomInfo && roomInfo.room && roomInfo.posX !== undefined && roomInfo.posY !== undefined) {
          const positionCheck = await roomService.isPositionAvailable(roomInfo.room, parseInt(roomInfo.posX), parseInt(roomInfo.posY));
          
          if (positionCheck.valid) {
            roomId = positionCheck.room.id;
            posX = parseInt(roomInfo.posX);
            posY = parseInt(roomInfo.posY);
          }
        }
        
        // Tạo positionInfo để truyền vào generateAndAssignAgentToken
        const positionInfo = {
          roomId: roomId,
          posX: posX,
          posY: posY
        };
        
        // Generate and assign a new token for the agent
        const plainToken = await computerService.generateAndAssignAgentToken(unique_agent_id, positionInfo);
        
        // Notify admins about token renewal
        websocketService.notifyAdminsAgentRegistered(unique_agent_id, computer.id);
        
        return res.status(200).json({
          status: 'success',
          agentToken: plainToken
        });
      }
      
      if (!computer || !computer.agent_token_hash) {
        // Kiểm tra nếu computer với unique_agent_id này đã tồn tại nhưng chưa có token
        if (computer && !computer.agent_token_hash) {
          // Kiểm tra xem vị trí có khớp với thông tin yêu cầu không
          const positionMatches = roomInfo && 
                                 roomInfo.posX === computer.pos_x && 
                                 roomInfo.posY === computer.pos_y &&
                                 await roomService.isRoomNameMatchesId(roomInfo.room, computer.room_id);
          
          if (positionMatches) {
            console.log('[AgentController] Agent exists without token at same position, generating token directly');
            
            // Tạo positionInfo để truyền vào generateAndAssignAgentToken
            const positionInfo = {
              roomId: computer.room_id,
              posX: computer.pos_x,
              posY: computer.pos_y
            };
            
            // Generate and assign a token for the agent
            const plainToken = await computerService.generateAndAssignAgentToken(unique_agent_id, positionInfo);
            
            // Notify admins about successful registration without MFA
            websocketService.notifyAdminsAgentRegistered(unique_agent_id, computer.id);
            
            return res.status(200).json({
              status: 'success',
              agentToken: plainToken
            });
          }
        }
        
        // Tiếp tục xử lý như bình thường nếu không phải trường hợp đặc biệt
        // Kiểm tra thông tin phòng và vị trí
        if (roomInfo && roomInfo.room && roomInfo.posX !== undefined && roomInfo.posY !== undefined) {
          // Chuyển đổi tọa độ sang số nếu cần
          const posX = parseInt(roomInfo.posX);
          const posY = parseInt(roomInfo.posY);
          
          // Kiểm tra vị trí trong phòng có khả dụng không
          const positionCheck = await roomService.isPositionAvailable(roomInfo.room, posX, posY);
          console.log('[AgentController] Position check result:', positionCheck);
          
          if (!positionCheck.valid) {
            // Vị trí không khả dụng, yêu cầu agent cung cấp thông tin khác
            return res.status(400).json({
              status: 'position_error',
              message: positionCheck.message
            });
          }
          
          // Cập nhật roomInfo với thông tin bổ sung từ phòng
          roomInfo.roomId = positionCheck.room.id;
          roomInfo.maxColumns = positionCheck.room.layout.columns;
          roomInfo.maxRows = positionCheck.room.layout.rows;
        } else {
          // Thiếu thông tin phòng hoặc vị trí
          return res.status(400).json({
            status: 'position_error',
            message: 'Vui lòng cung cấp đầy đủ thông tin phòng và vị trí (room, posX, posY)'
          });
        }
        
        // Generate MFA code for new registration
        const mfaCode = mfaService.generateAndStoreMfa(unique_agent_id);
        console.log('[AgentController] Generated MFA code:', { unique_agent_id, mfaCode, roomInfo });
        
        // Notify admins about the new MFA request with room info
        websocketService.notifyAdminsNewMfa(unique_agent_id, mfaCode, roomInfo);
        
        return res.status(200).json({
          status: 'mfa_required'
        });
      } else {
        // Agent exists but needs to authenticate
        console.log('[AgentController] Agent exists, authentication required');
        return res.status(200).json({
          status: 'authentication_required'
        });
      }
    } catch (error) {
      console.error('[AgentController] Error in handleIdentifyRequest:', error);
      next(error);
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
      const { unique_agent_id, mfaCode, roomInfo } = req.body;
      
      console.log('[AgentController] Received verify MFA request:', { unique_agent_id, roomInfo });
      
      if (!unique_agent_id || !mfaCode) {
        return res.status(400).json({
          status: 'error',
          message: 'Agent ID and MFA code are required'
        });
      }
      
      // Verify the MFA code
      const isValid = mfaService.verifyMfa(unique_agent_id, mfaCode);
      
      if (isValid) {
        // Chuẩn bị thông tin vị trí
        let positionInfo = null;
        
        if (roomInfo && roomInfo.roomId && roomInfo.posX !== undefined && roomInfo.posY !== undefined) {
          positionInfo = {
            roomId: roomInfo.roomId,
            posX: parseInt(roomInfo.posX),
            posY: parseInt(roomInfo.posY)
          };
        }
        
        // Generate and assign a token for the agent, cùng với thông tin vị trí
        const plainToken = await computerService.generateAndAssignAgentToken(unique_agent_id, positionInfo);
        
        // Get the computer for notification
        const computer = await computerService.findComputerByAgentId(unique_agent_id);
        
        // Notify admins about successful registration
        websocketService.notifyAdminsAgentRegistered(unique_agent_id, computer.id);
        
        return res.status(200).json({
          status: 'success',
          agentToken: plainToken
        });
      } else {
        return res.status(401).json({
          status: 'error',
          message: 'Invalid or expired MFA code'
        });
      }
    } catch (error) {
      console.error('[AgentController] Error in handleVerifyMfa:', error);
      next(error);
    }
  }

  /**
   * Handle agent status update
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleStatusUpdate(req, res, next) {
    try {
      // To be implemented in stage 5
      return res.status(200).json({
        status: 'success',
        message: 'Status updated'
      });
    } catch (error) {
      next(error);
    }
  }

  /**
   * Handle agent command result
   * @param {Object} req - Express request object
   * @param {Object} res - Express response object
   * @param {Function} next - Express next middleware function
   */
  async handleCommandResult(req, res, next) {
    try {
      // To be implemented in stage 5
      return res.status(200).json({
        status: 'success',
        message: 'Command result received'
      });
    } catch (error) {
      next(error);
    }
  }
}

module.exports = new AgentController();