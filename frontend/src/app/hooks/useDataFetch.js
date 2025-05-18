import { useState, useEffect, useCallback } from 'react';
import { message } from 'antd';
import { useAppDispatch } from './useReduxSelector';

/**
 * Hook to fetch data using Redux thunk actions
 * @param {Function} thunkAction - Redux thunk action creator
 * @param {any[]} [args=[]] - Arguments passed to thunk action
 * @param {Object} [options={}] - Options for data fetching
 * @param {boolean} [options.manual=false] - If true, don't fetch data automatically on mount
 * @param {Function} [options.onSuccess] - Callback when data fetch succeeds
 * @param {Function} [options.onError] - Callback when data fetch fails
 * @param {any[]} [options.dependencies=[]] - Dependencies that trigger data refetch
 * @returns {Object} State and helper functions
 * @property {boolean} loading - Whether data is currently loading
 * @property {string|null} error - Error message if fetch failed
 * @property {Function} executeFetch - Function to manually trigger fetch
 * @property {Function} refresh - Function to refresh data
 */
export const useReduxFetch = (thunkAction, args = [], options = {}) => {
  const {
    manual = false,
    onSuccess,
    onError,
    dependencies = [],
  } = options;

  const [loading, setLoading] = useState(!manual);
  const [error, setError] = useState(null);
  const [triggerCount, setTriggerCount] = useState(0);
  
  const { dispatch } = useAppDispatch();

  /**
   * Executes the fetch operation with memoized action
   * @returns {Promise} Promise resolving to fetch result
   */
  const executeFetch = useCallback(() => {
    setLoading(true);
    setError(null);
    
    return dispatch(thunkAction(...args))
      .unwrap()
      .then(result => {
        if (onSuccess) onSuccess(result);
        return result;
      })
      .catch(err => {
        const errorMsg = err?.message || 'Unable to fetch data';
        setError(errorMsg);
        message.error(errorMsg);
        if (onError) onError(err);
        throw err;
      })
      .finally(() => {
        setLoading(false);
      });
  }, [dispatch, thunkAction, args, onSuccess, onError]);

  // Effect to automatically run when dependencies change
  useEffect(() => {
    if (!manual) {
      executeFetch();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [manual, executeFetch, triggerCount, ...dependencies]);

  /**
   * Function to trigger data refresh
   */
  const refresh = useCallback(() => {
    setTriggerCount(prev => prev + 1);
  }, []);

  return {
    loading,
    error,
    executeFetch,
    refresh,
  };
}; 