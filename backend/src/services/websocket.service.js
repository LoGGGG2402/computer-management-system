/**
 * Service for WebSocket operations
 */
class WebSocketService {
  constructor() {
    this.io = null;
  }

  /**
   * Set the Socket.IO instance
   * @param {Object} io - The Socket.IO instance
   */
  setIo(io) {
    this.io = io;
  }

  /**
   * Notify admin users about a new MFA code for an agent
   * @param {string} agentId - The unique agent ID
   * @param {string} mfaCode - The generated MFA code
   */
  notifyAdminsNewMfa(agentId, mfaCode) {
    if (!this.io) return;

    // Emit to admin channel
    this.io.to('admin').emit('agent_mfa_requested', {
      agentId,
      mfaCode,
      timestamp: new Date()
    });
  }

  /**
   * Notify admin users when an agent has been successfully registered
   * @param {string} agentId - The unique agent ID
   * @param {number} computerId - The computer ID in the database
   */
  notifyAdminsAgentRegistered(agentId, computerId) {
    if (!this.io) return;

    // Emit to admin channel
    this.io.to('admin').emit('agent_registered', {
      agentId,
      computerId,
      timestamp: new Date()
    });
  }
}

module.exports = new WebSocketService();