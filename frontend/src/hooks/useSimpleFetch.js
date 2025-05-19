import { useState, useEffect, useCallback } from "react";
import { message } from "antd";

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
    errorMessage = "Failed to fetch data",
  } = options;

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(!manual);
  const [error, setError] = useState(null);
  const [trigger, setTrigger] = useState(0);

  const fetchData = useCallback(
    async (...args) => {
      setLoading(true);
      setError(null);
      try {
        const result = await fetchFunction(...args);
        setData(result);
        if (onSuccess) {
          onSuccess(result);
        }
        return result;
      } catch (err) {
        console.error("Fetch error:", err);
        const errMsg = err.message || errorMessage;
        setError(errMsg);
        message.error(errMsg);
        if (onError) {
          onError(err);
        }
        setData(null);
        throw err;
      } finally {
        setLoading(false);
      }
    },
    [fetchFunction, onSuccess, onError, errorMessage]
  );

  useEffect(() => {
    if (!manual) {
      fetchData();
    }
  }, [fetchData, manual, trigger, ...initialDependencies]);

  const refresh = useCallback(() => {
    setTrigger((prev) => prev + 1);
  }, []);

  const executeFetch = useCallback(
    (...args) => {
      return fetchData(...args);
    },
    [fetchData]
  );

  return {
    data,
    loading,
    error,
    refresh,
    executeFetch,
    setData,
  };
};
