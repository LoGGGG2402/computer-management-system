const express = require("express");
const roomController = require("../controllers/room.controller");
const { verifyToken } = require("../middleware/authUser");
const { authAccess } = require("../middleware/authAccess");

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// Room routes with appropriate access checks
// Get all rooms (filtered by user permission - handled in controller/service)
router.get("/", roomController.getAllRooms); // No specific middleware here, logic inside controller

// Get specific room by ID (check room access)
router.get(
  "/:roomId",
  authAccess({ checkRoomIdParam: true }),
  roomController.getRoomById
);

// Routes that require room access
router.put(
  "/:roomId",
  authAccess({ checkRoomIdParam: true }),
  roomController.updateRoom
);

// Admin-only routes below
// Apply admin check directly to admin-specific routes
const requireAdmin = authAccess({ requiredRole: "admin" });

router.post("/", requireAdmin, roomController.createRoom);
router.post("/:roomId/assign", requireAdmin, roomController.assignUsersToRoom);
router.post(
  "/:roomId/unassign",
  requireAdmin,
  roomController.unassignUsersFromRoom
);
router.get("/:roomId/users", requireAdmin, roomController.getUsersInRoom);

module.exports = router;
