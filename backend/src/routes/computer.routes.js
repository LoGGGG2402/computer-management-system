const express = require('express');
const computerController = require('../controllers/computer.controller');
const { verifyToken } = require('../middleware/authUser');
const { authAccess } = require('../middleware/authAccess');

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// Computer routes with appropriate access checks
// Get all computers (admin only)
router.get('/', authAccess({ requiredRole: 'admin' }), computerController.getAllComputers);

// Get specific computer by ID (check computer access)
router.get('/:computerId', authAccess({ checkComputerIdParam: true }), computerController.getComputerById);

// Computer error management routes (check computer access)
const requireComputerAccess = authAccess({ checkComputerIdParam: true });
router.get('/:computerId/errors', requireComputerAccess, computerController.getComputerErrors);
router.post('/:computerId/errors', requireComputerAccess, computerController.reportComputerError);
router.put('/:computerId/errors/:errorId/resolve', requireComputerAccess, computerController.resolveComputerError);

// Admin-only routes below
router.delete('/:computerId', authAccess({ requiredRole: 'admin' }), computerController.deleteComputer);

module.exports = router;