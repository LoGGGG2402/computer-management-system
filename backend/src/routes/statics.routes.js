const express = require('express');
const staticsController = require('../controllers/statics.controller');
const { verifyToken } = require('../middleware/authUser');
const { authAccess } = require('../middleware/authAccess');

const router = express.Router();

// Apply authentication and admin check middleware to all routes in this file
router.use(verifyToken);
router.use(authAccess({ requiredRole: 'admin' }));

// Define the route to get system statistics
router.get('/', staticsController.getSystemStats);

module.exports = router;
