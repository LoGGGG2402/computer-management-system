const express = require('express');
const adminController = require('../controllers/admin.controller');
const { verifyToken } = require('../middleware/authUser');
const { authAccess } = require('../middleware/authAccess');

const router = express.Router();

// Apply authentication and admin check middleware to all routes in this file
router.use(verifyToken);
router.use(authAccess({ requiredRole: 'admin' }));

// System statistics endpoint
router.get('/stats', adminController.getSystemStats);

// Additional admin routes can be added here

module.exports = router;