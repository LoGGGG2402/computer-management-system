const express = require('express');
const authController = require('../controllers/auth.controller');
const { verifyToken } = require('../middleware/authJwt');

const router = express.Router();

// Auth routes
router.post('/login', authController.handleLogin);
router.get('/me', verifyToken, authController.handleGetMe);

module.exports = router;