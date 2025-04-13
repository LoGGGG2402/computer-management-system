const express = require('express');
const computerController = require('../controllers/computer.controller');
const { verifyToken } = require('../middleware/authJwt');
const { isAdmin } = require('../middleware/authAdmin');
const { hasComputerAccess } = require('../middleware/authComputerAccess');

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// Computer routes with appropriate access checks
// Get all computers (filtered by user permission)
router.get('/', isAdmin, computerController.getAllComputers);

// Get specific computer by ID
router.get('/:id', hasComputerAccess, computerController.getComputerById);

// Admin-only routes below
router.delete('/:id', isAdmin, computerController.deleteComputer);

module.exports = router;