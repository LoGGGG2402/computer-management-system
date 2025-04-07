/**
 * Middleware for checking if user has admin role
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const isAdmin = (req, res, next) => {
  try {
    // req.user is set by the verifyToken middleware
    if (req.user && req.user.role === 'admin') {
      next();
      return;
    }

    return res.status(403).json({
      status: 'error',
      message: 'Require Admin Role!'
    });
  } catch (error) {
    return res.status(500).json({
      status: 'error',
      message: 'Unable to validate user role'
    });
  }
};

module.exports = {
  isAdmin
};