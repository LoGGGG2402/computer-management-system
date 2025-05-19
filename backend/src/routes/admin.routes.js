const express = require("express");
const adminController = require("../controllers/admin.controller");
const { verifyToken } = require("../middleware/authUser");
const { authAccess } = require("../middleware/authAccess");
const { uploadAgentPackage } = require("../middleware/uploadFileMiddleware");

const router = express.Router();

// Apply authentication and admin check middleware to all routes in this file
router.use(verifyToken);
router.use(authAccess({ requiredRole: "admin" }));

// System statistics endpoint
router.get("/stats", adminController.getSystemStats);

// Agent version management routes
router.post(
  "/agents/versions",
  uploadAgentPackage,
  adminController.handleAgentUpload
);
router.put(
  "/agents/versions/:versionId",
  adminController.setAgentVersionStability
);
router.get("/agents/versions", adminController.getAgentVersions);

// Additional admin routes can be added here

module.exports = router;
