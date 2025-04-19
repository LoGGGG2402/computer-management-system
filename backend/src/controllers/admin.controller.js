const AdminService = require("../services/admin.service");
const logger = require("../utils/logger");

/**
 * Controller for handling admin-related requests.
 */
class AdminController {
  /**
   * Get system-wide statistics.
   * Requires admin privileges.
   * @param {Object} req - Express request object.
   * @param {Object} res - Express response object.
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - System statistics object (only if status is 'success'):
   *     - totalUsers {number}
   *     - totalRooms {number}
   *     - totalComputers {number}
   *     - onlineComputers {number}
   *     - offlineComputers {number}
   *     - computersWithErrors {number}
   *     - unresolvedErrors {Array<Object>} - List of errors with details
   *   - message {string} - Error message (only if status is 'error')
   */
  async getSystemStats(req, res) {
    try {
      const stats = await AdminService.getSystemStats();

      logger.debug("System statistics retrieved successfully", {
        stats: {
          totalUsers: stats.totalUsers,
          totalComputers: stats.totalComputers,
          onlineComputers: stats.onlineComputers,
          computersWithErrors: stats.computersWithErrors,
        },
      });

      return res.status(200).json({
        status: "success",
        data: stats,
      });
    } catch (error) {
      logger.error("Failed to retrieve system statistics:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      return res.status(500).json({
        status: "error",
        message: error.message || "Failed to retrieve system statistics",
      });
    }
  }

  // Additional admin controller methods can be added here
}

module.exports = new AdminController();