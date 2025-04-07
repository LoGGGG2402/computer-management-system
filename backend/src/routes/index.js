const express = require('express');
const router = express.Router();

// Import route modules
const authRoutes = require('./auth.routes');
const userRoutes = require('./user.routes');
// const computerRoutes = require('./computer.routes');

// Define API routes
router.use('/auth', authRoutes);
router.use('/users', userRoutes);
// router.use('/computers', computerRoutes);

// Default route
router.get('/', (req, res) => {
  res.json({
    message: 'Welcome to Computer Management System API',
    version: '1.0.0'
  });
});

module.exports = router;