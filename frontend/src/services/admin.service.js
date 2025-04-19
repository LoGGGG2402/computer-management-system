import api from './api';

/**
 * Service for admin-related functionality including system statistics.
 */
class AdminService {
  /**
   * Get system-wide statistics.
   * Requires admin privileges.
   * @returns {Promise<Object>} System statistics object with:
   *   - totalUsers {number}
   *   - totalRooms {number}
   *   - totalComputers {number}
   *   - onlineComputers {number}
   *   - offlineComputers {number}
   *   - computersWithErrors {number}
   *   - unresolvedErrors {Array<Object>} List of unresolved errors with details.
   * @throws {Error} If fetching statistics fails.
   */
  async getSystemStats() {
    try {
      const response = await api.get('/admin/stats');
      return response.data.data;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch system statistics';
      console.error('Get system stats error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  // Additional admin methods can be added here
}

export default new AdminService();