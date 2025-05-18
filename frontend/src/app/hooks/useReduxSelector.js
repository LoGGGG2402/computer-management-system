import { useSelector, useDispatch } from 'react-redux';
import { useMemo, useCallback } from 'react';

/**
 * Custom hook that automatically memoizes selectors to prevent unnecessary re-renders
 * @param {Function} selector - Redux selector function
 * @param {any[]} [deps=[]] - Optional dependencies array for memoization
 * @returns {any} Selected state value
 */
export const useAppSelector = (selector, deps = []) => {
  const memoizedSelector = useMemo(() => selector, [selector, ...deps]);
  return useSelector(memoizedSelector);
};

/**
 * Custom hook to access dispatch with automatic action creator memoization
 * @returns {Object} Object containing dispatch function and utility functions
 * @property {Function} dispatch - Direct dispatch function
 * @property {Function} createActionDispatcher - Function to create memoized action dispatcher
 */
export const useAppDispatch = () => {
  const dispatch = useDispatch();

  /**
   * Direct dispatch function
   * @param {Object} action - Redux action to dispatch
   * @returns {Object} Dispatched action
   */
  const dispatchAction = useCallback(
    (action) => dispatch(action),
    [dispatch]
  );

  /**
   * Creates a memoized action dispatcher
   * @param {Function} actionCreator - Redux action creator function
   * @returns {Function} Memoized action dispatcher
   */
  const createActionDispatcher = useCallback(
    (actionCreator) => (...args) => dispatch(actionCreator(...args)),
    [dispatch]
  );

  return {
    dispatch: dispatchAction,
    createActionDispatcher,
  };
};

/**
 * Creates a hook to access Redux state for a specific slice
 * @param {string} sliceName - Name of the Redux slice
 * @param {string[]} fields - Fields to retrieve from the slice
 * @returns {Function} Hook function to access slice state
 */
const createStateHook = (sliceName, fields) => () => {
  const sliceState = useAppSelector((state) => state[sliceName]);
  const dispatch = useDispatch();
  
  return {
    ...Object.fromEntries(
      fields.map(field => [field, sliceState[field]])
    ),
    dispatch
  };
};

/**
 * Custom hook to access and handle authentication-related data
 * @returns {Object} Auth-related state and actions
 * @property {Object|null} user - Current user data
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Function} dispatch - Dispatch function
 */
export const useAuthState = createStateHook('auth', [
  'user', 'loading', 'error'
]);

/**
 * Custom hook to access and handle socket-related data
 * @returns {Object} Socket-related state and methods
 * @property {Object|null} instance - Socket instance
 * @property {boolean} connected - Connection status
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Object} events - Socket events
 * @property {string[]} onlineComputers - List of online computers
 * @property {string[]} offlineComputers - List of offline computers
 * @property {Object} computerStatuses - Computer status mapping
 * @property {Function} dispatch - Dispatch function
 */
export const useSocketState = createStateHook('socket', [
  'instance', 'connected', 'loading', 'error', 'events', 
  'onlineComputers', 'offlineComputers', 'computerStatuses'
]);

/**
 * Custom hook to access and handle computer-related data
 * @returns {Object} Computer-related state and methods
 * @property {Object[]} computers - List of computers
 * @property {Object|null} selectedComputer - Currently selected computer
 * @property {Object[]} computerErrors - List of computer errors
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Object} pagination - Pagination data
 * @property {Function} dispatch - Dispatch function
 */
export const useComputerState = createStateHook('computers', [
  'computers', 'selectedComputer', 'computerErrors', 
  'loading', 'error', 'pagination'
]);

/**
 * Custom hook to access and handle room-related data
 * @returns {Object} Room-related state and methods
 * @property {Object[]} rooms - List of rooms
 * @property {Object|null} selectedRoom - Currently selected room
 * @property {Object[]} roomComputers - Computers in selected room
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Object} pagination - Pagination data
 * @property {Function} dispatch - Dispatch function
 */
export const useRoomState = createStateHook('rooms', [
  'rooms', 'selectedRoom', 'roomComputers', 
  'loading', 'error', 'pagination'
]);

/**
 * Custom hook to access and handle user-related data
 * @returns {Object} User-related state and methods
 * @property {Object[]} users - List of users
 * @property {Object|null} selectedUser - Currently selected user
 * @property {Object[]} userRooms - Rooms assigned to selected user
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Object} pagination - Pagination data
 * @property {Function} dispatch - Dispatch function
 */
export const useUserState = createStateHook('users', [
  'users', 'selectedUser', 'userRooms', 
  'loading', 'error', 'pagination'
]);

/**
 * Custom hook to access and handle command-related data
 * @returns {Object} Command-related state and methods
 * @property {Object[]} pendingCommands - List of pending commands
 * @property {Object[]} commandHistory - Command history
 * @property {Object[]} commandResults - Command execution results
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {string[]} availableCommands - List of available commands
 * @property {Function} dispatch - Dispatch function
 */
export const useCommandState = createStateHook('command', [
  'pendingCommands', 'commandHistory', 'commandResults',
  'loading', 'error', 'availableCommands'
]);

/**
 * Custom hook to access and handle admin-related data
 * @returns {Object} Admin-related state and methods
 * @property {Object|null} systemStats - System statistics
 * @property {Object[]} systemLogs - System logs
 * @property {Object[]} auditLogs - Audit logs
 * @property {Object[]} errorLogs - Error logs
 * @property {Object} backupStatus - Backup status
 * @property {boolean} loading - Loading state
 * @property {string|null} error - Error message
 * @property {Object} pagination - Pagination data
 * @property {Function} dispatch - Dispatch function
 */
export const useAdminState = createStateHook('admin', [
  'systemStats', 'systemLogs', 'auditLogs', 'errorLogs',
  'backupStatus', 'loading', 'error', 'pagination'
]); 