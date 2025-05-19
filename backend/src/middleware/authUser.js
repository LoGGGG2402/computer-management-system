const jwt = require("jsonwebtoken");
const config = require("../config/auth.config.js");
const logger = require("../utils/logger.js");

/**
 * Verify JWT token from requests
 * @param {Object} req - Express request object
 * @param {Object} res - Express response object
 * @param {Function} next - Express next middleware function
 */
const verifyToken = (req, res, next) => {
  const token =
    req.headers["x-access-token"] ||
    req.headers["authorization"]?.replace("Bearer ", "");

  if (!token) {
    logger.debug("Missing authentication token", {
      endpoint: `${req.method} ${req.originalUrl}`,
      ip: req.ip,
    });

    return res.status(403).json({
      status: "error",
      message: "No token provided",
    });
  }

  jwt.verify(token, config.accessToken.secret, (err, decoded) => {
    if (err) {
      logger.warn("Invalid JWT token", {
        error: err.message,
        endpoint: `${req.method} ${req.originalUrl}`,
        ip: req.ip,
      });

      return res.status(401).json({
        status: "error",
        message: "Unauthorized (Invalid token)",
      });
    }

    req.user = {
      id: decoded.id,
      username: decoded.username,
      role: decoded.role,
    };

    logger.debug(
      `Authenticated user: ${decoded.username} (ID: ${decoded.id}, Role: ${decoded.role})`,
      {
        endpoint: `${req.method} ${req.originalUrl}`,
      }
    );

    next();
  });
};

module.exports = {
  verifyToken,
};
