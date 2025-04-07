const computerService = require('../services/computer.service');
const mfaService = require('../services/mfa.service');
const websocketService = require('../services/websocket.service');

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
      const { unique_agent_id } = req.body;
      
      if (!unique_agent_id) {
        return res.status(400).json({
          status: 'error',
          message: 'Agent ID is required'
        });
      }
      
      // Check if computer with this agent ID exists and has a token
      const computer = await computerService.findComputerByAgentId(unique_agent_id);
      
      if (!computer || !computer.agent_token_hash) {
        // Generate MFA code for new registration
        const mfaCode = mfaService.generateAndStoreMfa(unique_agent_id);
        
        // Notify admins about the new MFA request
        websocketService.notifyAdminsNewMfa(unique_agent_id, mfaCode);
        
        return res.status(200).json({
          status: 'mfa_required'
        });
      } else {
        // Agent exists but needs to authenticate
        return res.status(200).json({
          status: 'authentication_required'
        });
      }
    } catch (error) {
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
      const { unique_agent_id, mfaCode } = req.body;
      
      if (!unique_agent_id || !mfaCode) {
        return res.status(400).json({
          status: 'error',
          message: 'Agent ID and MFA code are required'
        });
      }
      
      // Verify the MFA code
      const isValid = mfaService.verifyMfa(unique_agent_id, mfaCode);
      
      if (isValid) {
        // Generate and assign a token for the agent
        const plainToken = await computerService.generateAndAssignAgentToken(unique_agent_id);
        
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