const db = require('../database/models');
const websocketService = require('./websocket.service.js');
const { User, Room, Computer } = db;

class AdminService {
  /**
   * Retrieves general statistics about the system.
   * This includes counts of users, rooms, computers, online/offline status,
   * computers with errors, and a list of unresolved errors.
   *
   * @returns {Promise<object>} A promise that resolves to an object containing system statistics:
   *   - `totalUsers` {number}: The total number of registered users.
   *   - `totalRooms` {number}: The total number of rooms.
   *   - `totalComputers` {number}: The total number of computers registered in the system.
   *   - `onlineComputers` {number}: The number of computers currently connected via WebSocket.
   *   - `offlineComputers` {number}: The calculated number of computers not currently connected (total - online).
   *   - `computersWithErrors` {number}: The number of computers flagged with active errors.
   *   - `unresolvedErrors` {Array<Object>}: A list of currently unresolved errors across all computers. Each object contains:
   *     - `computerId` {number}: ID of the computer with the error.
   *     - `computerName` {string}: Name of the computer with the error.
   *     - `errorId` {number}: Unique ID of the error instance.
   *     - `error_type` {string}: Type/category of the error.
   *     - `error_message` {string}: Human-readable error message.
   *     - `error_details` {Object}: Additional details about the error.
   *     - `reported_at` {Date}: When the error was reported.
   * @throws {Error} Throws an error if there's an issue querying the database
   *                 or retrieving the number of connected agents from the WebSocket service.
   *                 The error message will be 'Could not retrieve statistics data.'.
   */
  async getSystemStats() {
    try {
      const [
        totalUsers,
        totalRooms,
        totalComputers,
        computersWithErrorRecords,
      ] = await Promise.all([
        User.count(),
        Room.count(),
        Computer.count(),
        Computer.findAll({
          where: { have_active_errors: true },
          attributes: ['id', 'name', 'errors'] 
        }),
      ]);

      const unresolvedErrors = [];
      computersWithErrorRecords.forEach(computer => {
        const errors = Array.isArray(computer.errors) ? computer.errors : [];
        errors.forEach(error => {
          if (!error.resolved) {
            unresolvedErrors.push({
              computerId: computer.id,
              computerName: computer.name,
              errorId: error.id, 
              error_type: error.error_type,
              error_message: error.error_message,
              error_details: error.error_details,
              reported_at: error.reported_at,
            });
          }
        });
      });

      const onlineComputers = websocketService.numberOfConnectedAgents();
      const computersWithErrorsCount = computersWithErrorRecords.length;

      return {
        totalUsers,
        totalRooms,
        totalComputers,
        onlineComputers,
        offlineComputers: totalComputers - onlineComputers,
        computersWithErrors: computersWithErrorsCount, 
        unresolvedErrors,
      };
    } catch (error) {
      throw new Error('Could not retrieve statistics data.');
    }
  }

  // Additional admin service methods can be added here
}

module.exports = new AdminService();