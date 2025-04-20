const express = require('express');
const agentController = require('../controllers/agent.controller');
const { verifyAgentToken } = require('../middleware/authAgentToken');

const router = express.Router();

// Public agent routes (no authentication required)
router.post('/identify', agentController.handleIdentifyRequest);
router.post('/verify-mfa', agentController.handleVerifyMfa);

// Protected agent routes (require agent token authentication)
router.post('/hardware-info', verifyAgentToken, agentController.handleHardwareInfo);

// Agent update related routes
router.get('/check_update', verifyAgentToken, agentController.handleCheckUpdate);
router.post('/report-error', verifyAgentToken, agentController.handleErrorReport);

// Agent package download route
router.get('/agent-packages/:filename', verifyAgentToken, agentController.handleAgentPackageDownload);

module.exports = router;