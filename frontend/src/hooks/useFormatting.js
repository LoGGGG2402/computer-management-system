import { useCallback } from 'react';

/**
 * Custom hook providing common data formatting functions.
 * @returns {object} An object containing formatting functions.
 */
export const useFormatting = () => {
  /**
   * Formats a timestamp to a readable date and time string.
   * @param {string|number|Date} timestamp - The timestamp to format.
   * @returns {string} Formatted date and time or 'Invalid Date'/'Never'.
   */
  const formatTimestamp = useCallback((timestamp) => {
    if (!timestamp) return 'Never';
    try {
      const date = new Date(timestamp);
      if (isNaN(date.getTime())) return 'Invalid Date';
      return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    } catch (e) {
      return 'Invalid Date';
    }
  }, []);

  /**
   * Calculates a human-readable time ago string from a timestamp.
   * @param {string|number|Date} timestamp - The timestamp to calculate from.
   * @returns {string} Human-readable time ago string or 'Invalid Date'/'Never'.
   */
  const getTimeAgo = useCallback((timestamp) => {
    if (!timestamp) return 'Never';
    try {
      const now = new Date();
      const time = new Date(timestamp);
      if (isNaN(time.getTime())) return 'Invalid Date';

      const diffMs = now - time;
      const diffSecs = Math.floor(diffMs / 1000);
      if (diffSecs < 0) return 'In the future';
      if (diffSecs < 60) return `${diffSecs} second${diffSecs !== 1 ? 's' : ''} ago`;
      const diffMins = Math.floor(diffSecs / 60);
      if (diffMins < 60) return `${diffMins} minute${diffMins !== 1 ? 's' : ''} ago`;
      const diffHours = Math.floor(diffMins / 60);
      if (diffHours < 24) return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`;
      const diffDays = Math.floor(diffHours / 24);
      return `${diffDays} day${diffDays !== 1 ? 's' : ''} ago`;
    } catch (e) {
      return 'Invalid Date';
    }
  }, []);

  /**
   * Formats RAM size from bytes to GB.
   * @param {number|string} bytes - RAM size in bytes.
   * @returns {string} Formatted RAM size in GB or 'Unknown'.
   */
  const formatRAMSize = useCallback((bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  }, []);

  /**
   * Formats disk size from bytes to GB.
   * @param {number|string} bytes - Disk size in bytes.
   * @returns {string} Formatted disk size in GB or 'Unknown'.
   */
  const formatDiskSize = useCallback((bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  }, []);

  /**
   * Determines the color for status indicators based on percentage value.
   * @param {number} value - The percentage value (0-100).
   * @returns {string} Color code ('#52c41a', '#faad14', or '#f5222d').
   */
  const getStatusColor = useCallback((value) => {
    if (value < 60) return '#52c41a'; // Green
    if (value < 80) return '#faad14'; // Yellow
    return '#f5222d'; // Red
  }, []);

  return {
    formatTimestamp,
    getTimeAgo,
    formatRAMSize,
    formatDiskSize,
    getStatusColor,
  };
};
