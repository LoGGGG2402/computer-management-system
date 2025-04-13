const express = require('express');
const userController = require('../controllers/user.controller');
const { verifyToken } = require('../middleware/authJwt');
const { isAdmin } = require('../middleware/authAdmin');

const router = express.Router();

// Apply authentication middleware to all routes
router.use(verifyToken);

// All routes below require admin role
router.use(isAdmin);

// User CRUD routes
router.get('/', userController.getAllUsers);
router.get('/:id', userController.getUserById);
router.post('/', userController.createUser);
router.put('/:id', userController.updateUser);
router.delete('/:id', userController.deleteUser);

// User reactivation route
router.put('/:id/reactivate', userController.reactivateUser);

module.exports = router;