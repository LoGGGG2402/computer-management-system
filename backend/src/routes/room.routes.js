const express = require('express');
const roomController = require('../controllers/room.controller');
const computerController = require('../controllers/computer.controller');
const { verifyToken } = require('../middleware/authJwt');
const { isAdmin } = require('../middleware/authAdmin');
const { hasRoomAccess } = require('../middleware/authRoomAccess');

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// Room routes with appropriate access checks
// Get all rooms (filtered by user permission)
router.get('/', roomController.getAllRooms);

// Get specific room by ID (check room access)
router.get('/:id', hasRoomAccess, roomController.getRoomById);

// Routes that require room access
router.put('/:id', hasRoomAccess, roomController.updateRoom);

// Admin-only routes below
router.use('/admin', isAdmin);
router.post('/', isAdmin, roomController.createRoom);
router.delete('/:id', isAdmin, roomController.deleteRoom);

// User assignment routes (admin only)
router.post('/:roomId/assign', isAdmin, roomController.assignUsersToRoom);
router.post('/:roomId/unassign', isAdmin, roomController.unassignUsersFromRoom);
router.get('/:roomId/users', isAdmin, roomController.getUsersInRoom);

module.exports = router;