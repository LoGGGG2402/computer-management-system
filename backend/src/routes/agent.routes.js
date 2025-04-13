const express = require('express');
const agentController = require('../controllers/agent.controller');
const { verifyAgentToken } = require('../middleware/authAgentToken');

const router = express.Router();

// Public agent routes (no authentication required)
router.post('/identify', agentController.handleIdentifyRequest);
router.post('/verify-mfa', agentController.handleVerifyMfa);

// Protected agent routes (require agent token authentication)
router.post('/hardware-info', verifyAgentToken, agentController.handleHardwareInfo);

module.exports = router;