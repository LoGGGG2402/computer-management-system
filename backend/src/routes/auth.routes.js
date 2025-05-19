const express = require("express");
const authController = require("../controllers/auth.controller");
const { verifyToken } = require("../middleware/authUser");

const router = express.Router();

// Auth routes
router.post("/login", authController.handleLogin);
router.post("/refresh-token", authController.handleRefreshToken);
router.post("/logout", authController.handleLogout);

// Get current user
router.get("/me", verifyToken, authController.handleGetMe);

module.exports = router;
