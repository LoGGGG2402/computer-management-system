/**
 * @fileoverview Custom hook for clipboard operations
 * 
 * This hook provides functionality to copy text to the clipboard
 * and track the copy operation status.
 * 
 * @module useCopyToClipboard
 */
import { useState, useCallback } from 'react';

/**
 * Hook for copying text to clipboard
 * 
 * Provides:
 * - A copyToClipboard function that copies text to clipboard
 * - A copied state that tracks if the last copy operation was successful
 * - An error state for any copy operation failures
 * 
 * @returns {Object} The clipboard hook utilities
 * @returns {Function} copyToClipboard - Function to copy text to clipboard
 * @returns {boolean} copied - Whether last copy was successful
 * @returns {string|null} error - Error message if copy failed
 */
export const useCopyToClipboard = () => {
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState(null);

  /**
   * Copies text to clipboard using the Clipboard API
   * Falls back to document.execCommand if Clipboard API is unavailable
   * 
   * @function
   * @param {string} text - The text to copy to clipboard
   * @returns {Promise<boolean>} Whether the copy operation succeeded
   */
  const copyToClipboard = useCallback(async (text) => {
    try {
      if (!text) {
        throw new Error('No text provided to copy');
      }

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
        
        // Make the textarea non-visible
        textArea.style.position = 'fixed';
        textArea.style.top = '0';
        textArea.style.left = '0';
        textArea.style.width = '1px';
        textArea.style.height = '1px';
        textArea.style.opacity = '0';
        
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        
        // Execute copy command and clean up
        const success = document.execCommand('copy');
        document.body.removeChild(textArea);
        
        if (!success) {
          throw new Error('Unable to copy text to clipboard');
        }
      }

      // Copy was successful
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