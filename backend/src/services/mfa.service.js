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
    
    // Map to track agent_id to MFA code mapping for better management
    this.agentMfaMap = new Map();
  }

  /**
   * Generate and store a new MFA code for an agent
   * @param {string} agentId - The unique agent ID to generate MFA for
   * @returns {string} The generated MFA code
   */
  generateAndStoreMfa(agentId) {
    // Check if there's an existing MFA code for this agent
    if (this.agentMfaMap.has(agentId)) {
      // Get the old MFA code
      const oldMfaCode = this.agentMfaMap.get(agentId);
      
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
    const uniqueHash = this.generateUniqueHash(agentId, mfaCode);
    
    // Store in cache with the agent ID as key and an object containing both code and hash
    this.mfaCache.set(agentId, {
      code: mfaCode,
      hash: uniqueHash,
      generatedFor: agentId
    });
    
    // Track the agent to MFA code mapping
    this.agentMfaMap.set(agentId, mfaCode);
    
    console.log(`[MfaService] Generated new MFA code for agent: ${agentId}`);

    return mfaCode;
  }

  /**
   * Generate a unique hash binding an MFA code to a specific agent ID
   * @param {string} agentId - The unique agent ID 
   * @param {string} mfaCode - The MFA code
   * @returns {string} A hash that uniquely identifies this agent-MFA pair
   * @private
   */
  generateUniqueHash(agentId, mfaCode) {
    return crypto
      .createHash('sha256')
      .update(`${agentId}-${mfaCode}-${Date.now()}`)
      .digest('hex');
  }

  /**
   * Verify an MFA code for an agent
   * @param {string} agentId - The unique agent ID to verify MFA for
   * @param {string} code - The MFA code to verify
   * @returns {boolean} True if the code is valid, false otherwise
   */
  verifyMfa(agentId, code) {
    // Get the stored MFA info for this agent
    const storedMfaInfo = this.mfaCache.get(agentId);

    // If there's no stored code, verification fails
    if (!storedMfaInfo) {
      console.log(`[MfaService] No MFA code found for agent: ${agentId}`);
      return false;
    }

    // Ensure that this MFA code was generated specifically for this agent
    if (storedMfaInfo.generatedFor !== agentId) {
      console.log(`[MfaService] MFA code was not generated for this agent: ${agentId}`);
      return false;
    }

    // If the code matches, delete it from cache and return true
    if (storedMfaInfo.code === code) {
      this.mfaCache.del(agentId);
      this.agentMfaMap.delete(agentId);
      console.log(`[MfaService] MFA code verified and removed for agent: ${agentId}`);
      return true;
    }

    console.log(`[MfaService] Invalid MFA code attempt for agent: ${agentId}`);
    return false;
  }
  
  /**
   * Check if an agent has an active MFA code
   * @param {string} agentId - The unique agent ID to check
   * @returns {boolean} True if the agent has an active MFA code, false otherwise
   */
  hasActiveMfa(agentId) {
    return this.agentMfaMap.has(agentId);
  }
  
  /**
   * Invalidate an MFA code for an agent
   * @param {string} agentId - The unique agent ID to invalidate MFA for
   * @returns {boolean} True if an MFA code was invalidated, false if none existed
   */
  invalidateMfa(agentId) {
    if (this.agentMfaMap.has(agentId)) {
      this.mfaCache.del(agentId);
      this.agentMfaMap.delete(agentId);
      console.log(`[MfaService] MFA code manually invalidated for agent: ${agentId}`);
      return true;
    }
    return false;
  }
}

module.exports = new MfaService();