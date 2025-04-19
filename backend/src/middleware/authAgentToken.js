const computerService = require('../services/computer.service');
const logger = require('../utils/logger');

/**
 * Middleware to authenticate agent requests using agent token
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const verifyAgentToken = async (req, res, next) => {
  try {
    // Extract agent ID and token from headers
    const agentId = req.headers['agent-id'];
    const token = req.headers['agent-token'];

    // Check if both agent ID and token are provided
    if (!agentId || !token) {
      logger.debug('Missing agent credentials', { 
        hasAgentId: !!agentId,
        hasToken: !!token,
        endpoint: `${req.method} ${req.originalUrl}`, 
        ip: req.ip 
      });
      
      return res.status(403).json({
        status: 'error',
        message: 'Agent ID and token are required',
      });
    }

    // Verify the token with computer service
    const computer = await computerService.verifyAgentToken(agentId, token);
    
    if (!computer) {
      logger.warn('Invalid agent credentials', { 
        agentId,
        endpoint: `${req.method} ${req.originalUrl}`, 
        ip: req.ip
      });
      
      return res.status(401).json({
        status: 'error',
        message: 'Unauthorized (Invalid agent credentials)',
      });
    }

    // Attach computer object and agent ID to request for use in controllers
    req.computer = computer;
    req.agentId = agentId;
    
    logger.debug(`Authenticated agent: ${agentId} (Computer ID: ${computer.id})`, {
      endpoint: `${req.method} ${req.originalUrl}`
    });
    
    next();
  } catch (error) {
    logger.error('Agent authentication error:', {
      error: error.message,
      stack: error.stack,
      agentId: req.headers['agent-id'],
      endpoint: `${req.method} ${req.originalUrl}`,
      ip: req.ip
    });
    
    res.status(500).json({
      status: 'error',
      message: 'Internal server error during authentication',
    });
  }
};

module.exports = {
  verifyAgentToken,
};