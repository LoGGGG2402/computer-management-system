const NodeCache = require('node-cache');
const otpGenerator = require('otp-generator');
const crypto = require('crypto');

/**
 * Service for MFA (Multi-factor authentication) operations
 */
class MfaService {
  constructor() {
    // Create a cache with a default TTL of 5 minutes (300 seconds)
    this.mfaCache = new NodeCache({ stdTTL: 300 });
  }

  /**
   * Generate and store a new MFA code for an agent
   * @param {string} agentId - The unique agent ID to generate MFA for
   * @param {Object} positionInfo - The room information 
   * @returns {string} The generated MFA code
   */
  generateAndStoreMfa(agentId, positionInfo) {
    // Check if there's an existing MFA code for this agent
    if (this.mfaCache.has(agentId)) {
      // Remove the old MFA code from the cache
      this.mfaCache.del(agentId);
      
      console.log(`[MfaService] Invalidated previous MFA code for agent: ${agentId}`);
    }
    
    // Generate a 6-digit numeric OTP code
    const mfaCode = otpGenerator.generate(6, {
      digits: true,
      alphabets: false,
      upperCase: false,
      specialChars: false
    });

    // Create a unique key binding the MFA code to this specific agent
    // This prevents using a code generated for one agent with another agent_id
    const uniqueHash = crypto
    .createHash('sha256')
    .update(`${agentId}-${mfaCode}-${Date.now()}`)
    .digest('hex');
    
    // Store in cache with the agent ID as key and an object containing code, hash, positionInfo
    this.mfaCache.set(agentId, {
      code: mfaCode,
      hash: uniqueHash,
      generatedFor: agentId,
      positionInfo: positionInfo // Store the room information
    });
    
    console.log(`[MfaService] Generated new MFA code for agent: ${agentId} in room: ${positionInfo?.id || 'unknown'}`);

    return mfaCode;
  }

  /**
   * Verify an MFA code for an agent
   * @param {string} agentId - The unique agent ID to verify MFA for
   * @param {string} code - The MFA code to verify
   * @returns {boolean, Object} True if the code is valid, false otherwise
   * and the positionInfo object
   */
  verifyMfa(agentId, code) {
    // Get the stored MFA info for this agent
    const storedMfaInfo = this.mfaCache.get(agentId);

    // If there's no stored code, verification fails
    if (!storedMfaInfo) {
      console.log(`[MfaService] No MFA code found for agent: ${agentId}`);
      return { valid: false, positionInfo: null };
    }

    // Ensure that this MFA code was generated specifically for this agent
    if (storedMfaInfo.generatedFor !== agentId) {
      console.log(`[MfaService] MFA code was not generated for this agent: ${agentId}`);
      return { valid: false, positionInfo: null };
    }

    // If the code matches, delete it from cache and return true
    if (storedMfaInfo.code === code) {
      this.mfaCache.del(agentId);
      console.log(`[MfaService] MFA code verified and removed for agent: ${agentId}`);
      return { valid: true, positionInfo: storedMfaInfo.positionInfo };
    }

    console.log(`[MfaService] Invalid MFA code attempt for agent: ${agentId}`);
    return { valid: false, positionInfo: null };
  }
}

module.exports = new MfaService();