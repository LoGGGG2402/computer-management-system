const NodeCache = require('node-cache');
const otpGenerator = require('otp-generator');

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
   * @returns {string} The generated MFA code
   */
  generateAndStoreMfa(agentId) {
    // Generate a 6-digit numeric OTP code
    const mfaCode = otpGenerator.generate(6, {
      digits: true,
      alphabets: false,
      upperCase: false,
      specialChars: false
    });

    // Store the code in the cache with the agentId as the key
    this.mfaCache.set(agentId, mfaCode);

    return mfaCode;
  }

  /**
   * Verify an MFA code for an agent
   * @param {string} agentId - The unique agent ID to verify MFA for
   * @param {string} code - The MFA code to verify
   * @returns {boolean} True if the code is valid, false otherwise
   */
  verifyMfa(agentId, code) {
    // Get the stored MFA code for this agent
    const storedCode = this.mfaCache.get(agentId);

    // If the code matches, delete it from cache and return true
    if (storedCode && storedCode === code) {
      this.mfaCache.del(agentId);
      return true;
    }

    return false;
  }
}

module.exports = new MfaService();