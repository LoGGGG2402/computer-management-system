const NodeCache = require('node-cache');
const otpGenerator = require('otp-generator');
const crypto = require('crypto');

/**
 * Service for MFA (Multi-factor authentication) operations
 */
class MfaService {
  /**
   * Creates a new MFA service instance with a cache for storing temporary MFA codes
   */
  constructor() {
    this.mfaCache = new NodeCache({ stdTTL: 300 });
  }

  /**
   * Generate and store a new MFA code for an agent
   * @param {string} agentId - The unique agent ID to generate MFA for
   * @param {Object} positionInfo - The room information 
   * @param {number} [positionInfo.roomId] - The ID of the room where the agent is located
   * @param {number} [positionInfo.posX] - The X position in the room grid
   * @param {number} [positionInfo.posY] - The Y position in the room grid
   * @returns {string} The generated MFA code
   */
  generateAndStoreMfa(agentId, positionInfo) {
    if (this.mfaCache.has(agentId)) {
      this.mfaCache.del(agentId);
    }
    
    const mfaCode = otpGenerator.generate(6, {
      digits: true,
      alphabets: false,
      upperCase: false,
      specialChars: false
    });

    const uniqueHash = crypto
    .createHash('sha256')
    .update(`${agentId}-${mfaCode}-${Date.now()}`)
    .digest('hex');
    
    this.mfaCache.set(agentId, {
      code: mfaCode,
      hash: uniqueHash,
      generatedFor: agentId,
      positionInfo: positionInfo
    });

    return mfaCode;
  }

  /**
   * Verify an MFA code for an agent
   * @param {string} agentId - The unique agent ID to verify MFA for
   * @param {string} code - The MFA code to verify
   * @returns {Object} Verification result and position information
   * @returns {boolean} result.valid - True if the code is valid, false otherwise
   * @returns {Object|null} result.positionInfo - The stored position information if code is valid
   * @returns {number} [result.positionInfo.roomId] - The ID of the room where the agent is located
   * @returns {number} [result.positionInfo.posX] - The X position in the room grid
   * @returns {number} [result.positionInfo.posY] - The Y position in the room grid
   */
  verifyMfa(agentId, code) {
    const storedMfaInfo = this.mfaCache.get(agentId);
    
    if (!storedMfaInfo) {
      return { valid: false, positionInfo: null };
    }

    if (storedMfaInfo.generatedFor !== agentId) {
      return { valid: false, positionInfo: null };
    }

    if (storedMfaInfo.code === code) {
      this.mfaCache.del(agentId);
      return { valid: true, positionInfo: storedMfaInfo.positionInfo };
    }

    return { valid: false, positionInfo: null };
  }
}

module.exports = new MfaService();