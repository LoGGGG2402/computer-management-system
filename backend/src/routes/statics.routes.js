const express = require('express');
const staticsController = require('../controllers/statics.controller');
const { verifyToken } = require('../middleware/authJwt');
const { isAdmin } = require('../middleware/authAdmin');

const router = express.Router();

// Apply authentication and admin check middleware to all routes in this file
router.use(verifyToken);
router.use(isAdmin);

// Define the route to get system statistics
router.get('/', staticsController.getSystemStats);

module.exports = router;
