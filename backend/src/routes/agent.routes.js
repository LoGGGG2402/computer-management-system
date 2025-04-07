const express = require('express');
const agentController = require('../controllers/agent.controller');
const { verifyAgentToken } = require('../middleware/authAgentToken');

const router = express.Router();

// Public agent routes (no authentication required)
router.post('/identify', agentController.handleIdentifyRequest);
router.post('/verify-mfa', agentController.handleVerifyMfa);

// Protected agent routes (require agent token authentication)
router.put('/status', verifyAgentToken, agentController.handleStatusUpdate);
router.post('/command-result', verifyAgentToken, agentController.handleCommandResult);

module.exports = router;