import { useState, useCallback } from 'react';

/**
 * Custom hook for copying text to clipboard
 * @returns {Object} Object containing copyToClipboard function and state
 * @property {Function} copyToClipboard - Function to copy text to clipboard
 * @property {boolean} copied - Whether text was successfully copied
 * @property {string|null} error - Error message if copy failed
 */
export const useCopyToClipboard = () => {
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState(null);

  /**
   * Copies text to clipboard
   * @param {string} text - The text to copy to clipboard
   * @returns {Promise<boolean>} Whether the copy operation succeeded
   */
  const copyToClipboard = useCallback(async (text) => {
    try {
      if (!text) throw new Error('No text provided to copy');

      // Reset states
      setCopied(false);
      setError(null);

      // Try to use modern navigator.clipboard API if available
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
      } else {
        // Fallback for older browsers or non-secure contexts
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.cssText = 'position:fixed;top:0;left:0;width:1px;height:1px;opacity:0;';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        
        const success = document.execCommand('copy');
        document.body.removeChild(textArea);
        
        if (!success) throw new Error('Unable to copy text to clipboard');
      }

      setCopied(true);
      return true;
    } catch (err) {
      console.error('Error copying to clipboard:', err);
      setError(err.message || 'Failed to copy to clipboard');
      return false;
    }
  }, []);

  return { copyToClipboard, copied, error };
};

/**
 * Custom hook providing common data formatting functions
 * @returns {Object} Object containing formatting utility functions
 * @property {Function} formatTimestamp - Formats a timestamp to readable date/time
 * @property {Function} getTimeAgo - Calculates time ago from timestamp
 * @property {Function} formatRAMSize - Formats RAM size in bytes to human readable
 * @property {Function} formatDiskSize - Formats disk size in bytes to human readable
 * @property {Function} formatByteSize - Formats any byte size to human readable
 * @property {Function} getStatusColor - Gets color based on status percentage
 */
export const useFormatting = () => {
  /**
   * Formats a timestamp to a readable date and time string
   * @param {string|number} timestamp - Timestamp to format
   * @returns {string} Formatted date/time string
   */
  const formatTimestamp = useCallback((timestamp) => {
    if (!timestamp) return 'Never';
    try {
      const date = new Date(timestamp);
      if (isNaN(date.getTime())) return 'Invalid Date';
      return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    } catch {
      return 'Invalid Date';
    }
  }, []);

  /**
   * Calculates a human-readable time ago string from a timestamp
   * @param {string|number} timestamp - Timestamp to calculate from
   * @returns {string} Human readable time ago string
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
    } catch {
      return 'Invalid Date';
    }
  }, []);

  /**
   * Format size from bytes to human-readable units (KB, MB, GB)
   * @param {number} bytes - Size in bytes
   * @param {number} [decimals=2] - Number of decimal places
   * @returns {string} Formatted size string
   */
  const formatByteSize = useCallback((bytes, decimals = 2) => {
    if (!bytes) return 'Unknown';
    bytes = parseInt(bytes);
    
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    if (bytes === 0) return '0 B';
    
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    const formattedSize = parseFloat((bytes / Math.pow(1024, i)).toFixed(decimals));
    
    return `${formattedSize} ${sizes[i]}`;
  }, []);

  /**
   * Determines the color for status indicators based on percentage value
   * @param {number} value - Percentage value (0-100)
   * @returns {string} Color code for the status
   */
  const getStatusColor = useCallback((value) => {
    if (value < 60) return '#52c41a'; // Green
    if (value < 80) return '#faad14'; // Yellow
    return '#f5222d'; // Red
  }, []);

  return {
    formatTimestamp,
    getTimeAgo,
    formatRAMSize: (bytes) => formatByteSize(bytes), 
    formatDiskSize: (bytes) => formatByteSize(bytes),
    formatByteSize,
    getStatusColor,
  };
};

/**
 * Custom hook to manage modal state
 * @param {string} [initialAction='create'] - Initial action type ('create' or 'edit')
 * @returns {Object} Modal state and handler functions
 * @property {boolean} isModalVisible - Whether modal is visible
 * @property {Object|null} selectedItem - Currently selected item
 * @property {string} modalAction - Current modal action ('create' or 'edit')
 * @property {Function} openModal - Function to open modal
 * @property {Function} closeModal - Function to close modal
 * @property {Function} setIsModalVisible - Function to set modal visibility
 * @property {Function} setSelectedItem - Function to set selected item
 * @property {Function} setModalAction - Function to set modal action
 */
export const useModalState = (initialAction = 'create') => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedItem, setSelectedItem] = useState(null);
  const [modalAction, setModalAction] = useState(initialAction);

  /**
   * Opens the modal with specified action and item
   * @param {string} action - Action type ('create' or 'edit')
   * @param {Object|null} [item=null] - Item to edit
   */
  const openModal = useCallback((action, item = null) => {
    setModalAction(action);
    setSelectedItem(item);
    setIsModalVisible(true);
  }, []);

  /**
   * Closes the modal and resets state
   */
  const closeModal = useCallback(() => {
    setIsModalVisible(false);
    setSelectedItem(null);
  }, []);

  return {
    isModalVisible,
    selectedItem,
    modalAction,
    openModal,
    closeModal,
    setIsModalVisible,
    setSelectedItem,
    setModalAction,
  };
}; 