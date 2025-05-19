import { useState, useEffect, useCallback } from 'react';
import { message } from 'antd';

/**
 * Custom hook for simplifying data fetching, loading, and error handling.
 * @param {Function} fetchFunction - The async function that fetches data (e.g., service call).
 * @param {Array} initialDependencies - Dependencies that trigger the initial fetch.
 * @param {object} [options={}] - Configuration options.
 * @param {boolean} [options.manual=false] - If true, fetch won't run automatically on mount/dependency change.
 * @param {Function} [options.onSuccess] - Callback function on successful fetch.
 * @param {Function} [options.onError] - Callback function on fetch error.
 * @param {string} [options.errorMessage='Failed to fetch data'] - Default error message.
 * @returns {object} Contains data, loading state, error state, and fetch/refresh functions.
 */
export const useSimpleFetch = (
  fetchFunction,
  initialDependencies = [],
  options = {}
) => {
  const {
    manual = false,
    onSuccess,
    onError,
    errorMessage = 'Failed to fetch data',
  } = options;

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(!manual);
  const [error, setError] = useState(null);
  // Internal state to trigger refetch
  const [trigger, setTrigger] = useState(0);

  const fetchData = useCallback(async (...args) => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchFunction(...args);
      setData(result);
      if (onSuccess) {
        onSuccess(result);
      }
      return result; // Return result for manual calls
    } catch (err) {
      console.error('Fetch error:', err);
      const errMsg = err.message || errorMessage;
      setError(errMsg);
      message.error(errMsg);
      if (onError) {
        onError(err);
      }
      setData(null); // Reset data on error
      throw err; // Re-throw for further handling if needed
    } finally {
      setLoading(false);
    }
  }, [fetchFunction, onSuccess, onError, errorMessage]);

  // Effect for automatic fetching on mount and dependency/trigger changes
  useEffect(() => {
    if (!manual) {
      fetchData();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fetchData, manual, trigger, ...initialDependencies]);

  // Function to manually trigger a refetch
  const refresh = useCallback(() => {
    setTrigger(prev => prev + 1);
  }, []);

  // Function to manually fetch (useful when manual=true)
  const executeFetch = useCallback((...args) => {
     return fetchData(...args);
  }, [fetchData]);

  return {
    data,
    loading,
    error,
    refresh, // Use this to re-run the fetch with original dependencies
    executeFetch, // Use this to run the fetch manually, potentially with new arguments
    setData, // Allow external updates to data if necessary
  };
};
