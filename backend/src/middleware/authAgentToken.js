const computerService = require('../services/computer.service');

/**
 * Middleware to authenticate agent requests using agent token
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const verifyAgentToken = async (req, res, next) => {
  try {
    // Extract agent ID from headers
    const agentId = req.headers['x-agent-id'];
    
    // Extract token from authorization header
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.startsWith('Bearer ') 
      ? authHeader.slice(7) // Remove 'Bearer ' prefix
      : null;
    
    // Check if both agent ID and token are provided
    if (!agentId || !token) {
      return res.status(401).json({
        status: 'error',
        message: 'Agent ID and token are required'
      });
    }
    
    // Verify the token with computer service
    const computerId = await computerService.verifyAgentToken(agentId, token);
    
    if (computerId) {
      // Attach computer ID to request for use in controllers
      req.computerId = computerId;
      next();
    } else {
      // Return error for invalid token
      return res.status(401).json({
        status: 'error',
        message: 'Invalid Agent Token'
      });
    }
  } catch (error) {
    console.error('Agent authentication error:', error);
    return res.status(500).json({
      status: 'error',
      message: 'Authentication failed due to server error'
    });
  }
};

module.exports = { verifyAgentToken };