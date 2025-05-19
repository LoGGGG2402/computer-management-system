const express = require("express");
const userController = require("../controllers/user.controller");
const { verifyToken } = require("../middleware/authUser");
const { authAccess } = require("../middleware/authAccess");

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// All routes below require admin role
router.use(authAccess({ requiredRole: "admin" }));

// User CRUD routes
router.get("/", userController.getAllUsers);
router.get("/:userId", userController.getUserById);
router.post("/", userController.createUser);
router.put("/:userId", userController.updateUser);
router.delete("/:userId", userController.deleteUser);

// User reactivation route
router.put("/:userId/reactivate", userController.reactivateUser);

module.exports = router;
