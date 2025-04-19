const express = require('express');
const router = express.Router();

// Import route modules
const authRoutes = require('./auth.routes');
const userRoutes = require('./user.routes');
const roomRoutes = require('./room.routes');
const computerRoutes = require('./computer.routes');
const agentRoutes = require('./agent.routes');
const adminRoutes = require('./admin.routes');

// Define API routes
router.use('/auth', authRoutes);
router.use('/users', userRoutes);
router.use('/rooms', roomRoutes);
router.use('/computers', computerRoutes);
router.use('/agent', agentRoutes);
router.use('/admin', adminRoutes);

// Default route
router.get('/', (req, res) => {
  res.json({
    message: 'Welcome to Computer Management System API',
    version: '1.0.0'
  });
});

module.exports = router;