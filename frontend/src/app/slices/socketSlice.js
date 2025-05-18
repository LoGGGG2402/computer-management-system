/**
 * WebSocket management slice for Redux store
 * @module socketSlice
 */

import { createSlice } from '@reduxjs/toolkit';
import { io } from 'socket.io-client';

/**
 * Initial state for socket slice
 * @type {Object}
 */
const initialState = {
  socket: null,
  isConnected: false,
  isConnecting: false,
  error: null,
  computerStatuses: {}, // Store computer statuses {computerId: {status, cpuUsage, ramUsage, diskUsage, timestamp}}
  subscribedComputers: [], // List of subscribed computer IDs
  computerErrors: {}, // Computer errors by computerId
  events: [], // Store important events for debugging
  pendingAgentMFA: null, // Store MFA information waiting for verification
  registeredAgents: [] // Store recently registered agents
};

/**
 * Socket slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const socketSlice = createSlice({
  name: 'socket',
  initialState,
  reducers: {
    connectStart(state) {
      state.isConnecting = true;
      state.error = null;
    },
    connectSuccess(state, action) {
      state.socket = action.payload; // Store socket reference
      state.isConnected = true;
      state.isConnecting = false;
      state.error = null;
      state.events.push({
        type: 'CONNECTED',
        timestamp: new Date().toISOString()
      });
    },
    connectFail(state, action) {
      state.isConnected = false;
      state.isConnecting = false;
      state.error = action.payload;
      state.events.push({
        type: 'CONNECTION_ERROR',
        error: action.payload,
        timestamp: new Date().toISOString()
      });
    },
    disconnected(state) {
      state.isConnected = false;
      state.error = null;
      state.events.push({
        type: 'DISCONNECTED',
        timestamp: new Date().toISOString()
      });
    },
    updateComputerStatus(state, action) {
      const { computerId, status, cpuUsage, ramUsage, diskUsage, timestamp } = action.payload;
      state.computerStatuses[computerId] = { status, cpuUsage, ramUsage, diskUsage, timestamp };
      state.events.push({
        type: 'COMPUTER_STATUS_UPDATE',
        computerId,
        status,
        timestamp: new Date().toISOString()
      });
    },
    subscribeToComputer(state, action) {
      const computerId = action.payload;
      if (!state.subscribedComputers.includes(computerId)) {
        state.subscribedComputers.push(computerId);
      }
    },
    unsubscribeFromComputer(state, action) {
      const computerId = action.payload;
      state.subscribedComputers = state.subscribedComputers.filter(id => id !== computerId);
    },
    addComputerError(state, action) {
      const { computerId, error } = action.payload;
      if (!state.computerErrors[computerId]) {
        state.computerErrors[computerId] = [];
      }
      state.computerErrors[computerId].push(error);
    },
    resolveComputerError(state, action) {
      const { computerId, errorId } = action.payload;
      if (state.computerErrors[computerId]) {
        state.computerErrors[computerId] = state.computerErrors[computerId].filter(
          error => error.id !== errorId
        );
      }
    },
    clearErrors(state) {
      state.error = null;
    },
    clearSocketError(state) {
      state.error = null;
    },
    // Handle new MFA event
    receiveNewAgentMFA(state, action) {
      const { mfaCode, positionInfo, timestamp } = action.payload;
      state.pendingAgentMFA = {
        mfaCode,
        positionInfo,
        timestamp
      };
      state.events.push({
        type: 'NEW_AGENT_MFA',
        mfaCode,
        positionInfo,
        timestamp: new Date().toISOString()
      });
    },
    // Handle agent registered event
    receiveAgentRegistered(state, action) {
      const { computerId, positionInfo, timestamp } = action.payload;
      state.registeredAgents.unshift({
        computerId,
        positionInfo,
        timestamp
      });
      // Limit number of stored registered agents
      if (state.registeredAgents.length > 10) {
        state.registeredAgents = state.registeredAgents.slice(0, 10);
      }
      state.events.push({
        type: 'AGENT_REGISTERED',
        computerId,
        positionInfo,
        timestamp: new Date().toISOString()
      });
    },
    // Clear processed MFA information
    clearPendingAgentMFA(state) {
      state.pendingAgentMFA = null;
    }
  }
});

/**
 * Action creators for socket slice
 */
export const {
  connectStart,
  connectSuccess,
  connectFail,
  disconnected,
  updateComputerStatus,
  subscribeToComputer,
  unsubscribeFromComputer,
  addComputerError,
  resolveComputerError,
  clearErrors,
  clearSocketError,
  receiveNewAgentMFA,
  receiveAgentRegistered,
  clearPendingAgentMFA
} = socketSlice.actions;

/**
 * Error message mappings for socket errors
 * @type {Object}
 */
const SOCKET_ERROR_MESSAGES = {
  'Authentication failed: Invalid token': 'Invalid token',
  'Authentication failed: Token expired': 'Token expired',
  'Authentication failed: Missing X-Client-Type header': 'Missing X-Client-Type header',
  'Authentication failed: Invalid X-Client-Type header': 'Invalid X-Client-Type header',
  'Authentication failed: Missing Authorization header': 'Missing Authorization header',
  'Authentication failed: User account is deactivated': 'User account is deactivated',
  'Connection refused: Server is at capacity': 'Server is at capacity',
  'Connection refused: Rate limit exceeded': 'Rate limit exceeded',
  'Connection refused: Maintenance mode active': 'Server is in maintenance mode',
  'Internal error: Unable to establish WebSocket connection': 'WebSocket connection error'
};

/**
 * Thunk to initialize socket connection
 * @param {string} token - Authentication token
 * @returns {Function} Thunk function
 */
export const initializeSocket = (token) => (dispatch) => {
  dispatch(connectStart());

  try {
    const socketUrl = import.meta.env.VITE_API_URL || 'http://localhost:3000';
    
    const socket = io(socketUrl, {
      extraHeaders: {
        'X-Client-Type': 'frontend',
        'Authorization': `Bearer ${token}`
      },
      transports: ['websocket'],
      autoConnect: true,
      reconnection: true,
      reconnectionAttempts: 5,
      reconnectionDelay: 1000 
    });

    // Socket event listeners
    socket.on('connect', () => {
      dispatch(connectSuccess(socket));
    });

    socket.on('connect_error', (error) => {
      console.error('Socket connect error:', error);
      const errorMessage = SOCKET_ERROR_MESSAGES[error.message] || error.message;
      dispatch(connectFail(errorMessage));
    });

    socket.on('disconnect', (reason) => {
      console.log('Socket disconnected:', reason);
      if (reason === 'io server disconnect') {
        // Server disconnected, try to refresh token
        import('../services/auth.service').then(({ default: authService }) => {
          authService.refreshToken()
            .then(() => {
              // Reconnect after successful token refresh
              socket.connect();
            })
            .catch(() => {
              // If refresh fails, logout and redirect to login page
              authService.logout();
              window.location.href = '/login';
            });
        });
      } else {
        dispatch(disconnected());
      }
    });

    // Update computer status
    socket.on('computer:status_updated', (data) => {
      dispatch(updateComputerStatus(data));
    });

    // Add admin event handling
    socket.on('admin:new_agent_mfa', (data) => {
      dispatch(receiveNewAgentMFA(data));
    });

    socket.on('admin:agent_registered', (data) => {
      dispatch(receiveAgentRegistered(data));
    });

    // Return cleanup function to avoid memory leaks
    return () => {
      if (socket) {
        socket.disconnect();
      }
    };
  } catch (error) {
    console.error('Socket initialization error:', error);
    dispatch(connectFail(error.message));
    return null;
  }
};

/**
 * Thunk để ngắt kết nối socket
 * @returns {Function} Thunk function
 */
export const disconnectSocket = () => (dispatch, getState) => {
  const { socket } = getState().socket;
  if (socket) {
    socket.disconnect();
  }
  dispatch(disconnected());
};

/**
 * Thunk để đăng ký theo dõi máy tính
 * @param {string|number} computerId - Computer ID to subscribe to
 * @returns {Promise} Promise resolving to subscription result
 */
export const subscribeToComputerThunk = (computerId) => (dispatch, getState) => {
  const { socket, isConnected } = getState().socket;
  
  if (!socket || !isConnected) {
    return Promise.reject(new Error('Socket not connected'));
  }

  return new Promise((resolve, reject) => {
    socket.emit('frontend:subscribe', { computerId }, (response) => {
      if (response.status === 'success') {
        dispatch(subscribeToComputer(computerId));
        resolve(response);
      } else {
        reject(new Error(response.message || 'Failed to subscribe to computer'));
      }
    });
  });
};

/**
 * Thunk để hủy đăng ký theo dõi máy tính
 * @param {string|number} computerId - Computer ID to unsubscribe from
 * @returns {Promise} Promise resolving to unsubscription result
 */
export const unsubscribeFromComputerThunk = (computerId) => (dispatch, getState) => {
  const { socket, isConnected } = getState().socket;
  
  if (!socket || !isConnected) {
    return Promise.reject(new Error('Socket not connected'));
  }

  return new Promise((resolve, reject) => {
    socket.emit('frontend:unsubscribe', { computerId }, (response) => {
      if (response.status === 'success') {
        dispatch(unsubscribeFromComputer(computerId));
        resolve(response);
      } else {
        reject(new Error(response.message || 'Failed to unsubscribe from computer'));
      }
    });
  });
};

/**
 * Socket middleware to handle events
 * @param {Object} params - Middleware parameters
 * @returns {Function} Middleware function
 */
export const socketMiddleware = ({ getState }) => next => action => {
  // Handle socket-related tasks
  if (action.type === 'command/sendCommand') {
    const { socket, isConnected } = getState().socket;
    const { computerId, command, commandType } = action.payload;
    
    if (socket && isConnected) {
      socket.emit('frontend:send_command', {
        computerId,
        command,
        commandType
      });
    }
  }
  
  return next(action);
};

/**
 * Selectors for socket state
 */
export const selectSocketInstance = (state) => state.socket.socket;
export const selectSocketConnected = (state) => state.socket.isConnected;
export const selectSocketLoading = (state) => state.socket.isConnecting;
export const selectSocketError = (state) => state.socket.error;
export const selectSocketEvents = (state) => state.socket.events;
export const selectComputerStatuses = (state) => state.socket.computerStatuses;
export const selectComputerStatus = (state, computerId) => state.socket.computerStatuses[computerId];
export const selectOnlineComputers = (state) => {
  const { computerStatuses } = state.socket;
  return Object.keys(computerStatuses)
    .filter(id => computerStatuses[id].status === 'online')
    .map(id => parseInt(id));
};
export const selectOfflineComputers = (state) => {
  const { computerStatuses } = state.socket;
  return Object.keys(computerStatuses)
    .filter(id => computerStatuses[id].status === 'offline')
    .map(id => parseInt(id));
};
export const selectSocketComputerErrors = (state) => state.socket.computerErrors;
export const selectPendingAgentMFA = (state) => state.socket.pendingAgentMFA;
export const selectRegisteredAgents = (state) => state.socket.registeredAgents;

export default socketSlice.reducer; 