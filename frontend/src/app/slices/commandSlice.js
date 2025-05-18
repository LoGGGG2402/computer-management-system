/**
 * Command management slice for Redux store
 * @module commandSlice
 */

import { createSlice } from "@reduxjs/toolkit";

/**
 * Initial state for command slice
 * @type {Object}
 */
const initialState = {
  loading: false,
  error: null,
  availableCommands: [
    { type: "console", label: "Console Command" },
    { type: "powershell", label: "PowerShell" },
    { type: "cmd", label: "Command Prompt" },
    { type: "bash", label: "Bash" },
    { type: "system", label: "System Command" },
    { type: "service", label: "Service Command" },
  ],
  pendingCommands: {}, // {commandId: {computerId, command, commandType, timestamp}}
  commandHistory: {}, // {computerId: [{id, command, type, success, result, timestamp}]}
  currentResults: {}, // {commandId: {stdout, stderr, exitCode}}
};

/**
 * Valid command types for validation
 * @type {string[]}
 */
const validCommandTypes = [
  "console",
  "powershell",
  "cmd",
  "bash",
  "system",
  "service",
];

/**
 * Maximum allowed length for commands
 * @type {number}
 */
const MAX_COMMAND_LENGTH = 2000;

/**
 * Command slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const commandSlice = createSlice({
  name: "command",
  initialState,
  reducers: {
    /**
     * Start sending a command
     * @param {Object} state - Current state
     */
    sendCommandStart(state) {
      state.loading = true;
      state.error = null;
    },
    /**
     * Command sent successfully
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    sendCommandSuccess(state, action) {
      const { computerId, commandId, command, commandType } = action.payload;
      state.pendingCommands[commandId] = {
        computerId,
        command,
        commandType,
        timestamp: new Date().toISOString(),
      };
      state.loading = false;
    },
    /**
     * Command sending failed
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    sendCommandFail(state, action) {
      state.loading = false;
      state.error = action.payload;
    },
    /**
     * Add a new pending command
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    addPendingCommand(state, action) {
      const { commandId, computerId, command, commandType } = action.payload;
      state.pendingCommands[commandId] = {
        computerId,
        command,
        commandType,
        timestamp: new Date().toISOString(),
      };
    },
    /**
     * Update status of a pending command
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    updateCommandStatus(state, action) {
      const { commandId, status } = action.payload;
      if (state.pendingCommands[commandId]) {
        state.pendingCommands[commandId].status = status;
      }
    },
    /**
     * Receive result of a command execution
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    receiveCommandResult(state, action) {
      const { commandId, computerId, type, success, result, timestamp } =
        action.payload;

      // Save result to currentResults
      state.currentResults[commandId] = result;

      // Remove from pendingCommands
      delete state.pendingCommands[commandId];

      // Add to command history
      if (!state.commandHistory[computerId]) {
        state.commandHistory[computerId] = [];
      }

      state.commandHistory[computerId].unshift({
        id: commandId,
        command: state.pendingCommands[commandId]?.command || "",
        type,
        success,
        result,
        timestamp,
      });

      // Limit command history for each computer
      if (state.commandHistory[computerId].length > 20) {
        state.commandHistory[computerId] = state.commandHistory[
          computerId
        ].slice(0, 20);
      }
    },
    /**
     * Clear command history for a computer or all computers
     * @param {Object} state - Current state
     * @param {Object} action - Action payload
     */
    clearCommandHistory(state, action) {
      const computerId = action.payload;
      if (computerId) {
        state.commandHistory[computerId] = [];
      } else {
        state.commandHistory = {};
      }
    },
    /**
     * Clear command error
     * @param {Object} state - Current state
     */
    clearCommandError(state) {
      state.error = null;
    },
  },
});

/**
 * Action creators for command slice
 */
export const {
  sendCommandStart,
  sendCommandSuccess,
  sendCommandFail,
  addPendingCommand,
  updateCommandStatus,
  receiveCommandResult,
  clearCommandHistory,
  clearCommandError,
} = commandSlice.actions;

/**
 * Thunk to send a command to a computer
 * @param {string|number} computerId - Computer ID to send command to
 * @param {string} command - Command to execute
 * @param {string} commandType - Type of command (default: "console")
 * @returns {Function} Thunk function
 */
export const sendCommand =
  (computerId, command, commandType = "console") =>
  (dispatch, getState) => {
    // Validate command type
    if (!validCommandTypes.includes(commandType)) {
      return Promise.reject(new Error("Invalid command type"));
    }

    // Validate command length
    if (!command || command.length > MAX_COMMAND_LENGTH) {
      return Promise.reject(
        new Error("Command must be between 1 and 2000 characters")
      );
    }

    dispatch(sendCommandStart());

    const socket = getState().socket.socket;
    if (!socket || !getState().socket.isConnected) {
      return dispatch(sendCommandFail("Socket not connected"));
    }

    return new Promise((resolve, reject) => {
      socket.emit(
        "frontend:send_command",
        { computerId, command, commandType },
        (response) => {
          if (response.status === "success") {
            dispatch(
              sendCommandSuccess({
                computerId,
                commandId: response.commandId,
                command,
                commandType: response.commandType || commandType,
              })
            );
            resolve(response);
          } else {
            dispatch(
              sendCommandFail(response.message || "Failed to send command")
            );
            reject(new Error(response.message || "Failed to send command"));
          }
        }
      );
    });
  };

/**
 * Command middleware to handle socket events
 * @param {Object} params - Middleware parameters
 * @returns {Function} Middleware function
 */
export const commandSocketMiddleware =
  ({ dispatch }) =>
  (next) =>
  (action) => {
    // Handle socket events to update command status
    if (action.type === "socket/connected") {
      const socket = action.payload;

      // Listen for completed command results
      socket.on("command:completed", (data) => {
        const { commandId, computerId, type, success, result, timestamp } =
          data;
        dispatch(
          receiveCommandResult({
            commandId,
            computerId,
            type,
            success,
            result,
            timestamp,
          })
        );
      });
    }

    return next(action);
  };

/**
 * Selectors for command state
 */
export const selectCommandLoading = (state) => state.command.loading;
export const selectCommandError = (state) => state.command.error;
export const selectAvailableCommands = (state) =>
  state.command.availableCommands;
export const selectPendingCommands = (state) => state.command.pendingCommands;
export const selectCommandHistory = (state, computerId) =>
  state.command.commandHistory[computerId] || [];
export const selectCommandResult = (state, commandId) =>
  state.command.currentResults[commandId];

export default commandSlice.reducer;
