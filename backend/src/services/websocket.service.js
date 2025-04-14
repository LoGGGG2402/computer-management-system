/**
 * Service for WebSocket operations, optimized for clarity and maintainability.
 * Standardized based on agent's communication patterns.
 * Uses Map for realtime status and removes status on final disconnect.
 */

// --- Constants ---
const ROOM_PREFIXES = {
  ADMIN: 'admin_room',
  AGENT: (id) => `agent_${id}`,
  COMPUTER_SUBSCRIBERS: (id) => `computer_${id}`,
  USER: (id) => `user_${id}`,
};

const EVENTS = {
// Admin specific
  ADMIN_NEW_AGENT_MFA: 'admin:new_agent_mfa',
  ADMIN_AGENT_REGISTERED: 'admin:agent_registered',
  COMMAND_EXECUTE: 'command:execute',
  COMPUTER_STATUS_UPDATED: 'computer:status_updated',
  COMMAND_COMPLETED: 'command:completed',
};

const PENDING_COMMAND_TIMEOUT_MS = 5 * 60 * 1000;
const AGENT_OFFLINE_CHECK_DELAY_MS = 1500;

class WebSocketService {

  constructor() {
    this.io = null;
    this.computerService = null;
    this.agentRealtimeStatus = new Map();
    this.pendingCommands = new Map();

    console.info('WebSocketService initialized (using Map for agent status)');
  }

  /**
   * Sets the Socket.IO server instance.
   * @param {import("socket.io").Server} io - The Socket.IO Server instance.
   */
  setIo(io) {
    if (!io) {
      console.error('Attempted to set null or undefined Socket.IO instance');
      return;
    }
    this.io = io;
    console.info('Socket.IO instance has been set in WebSocketService');
  }


  /**
   * Emits an event to a specific room.
   * @private
   * @param {string} room - The room name.
   * @param {string} eventName - The event name.
   * @param {object} data - The data to emit.
   */
  _emitToRoom(room, eventName, data) {
    try {
      if (!this.io) {
        const errorMsg = 'WebSocket IO not initialized. Call setIo() first.';
        console.error(errorMsg);
        throw new Error(errorMsg);
      }
      this.io.to(room).emit(eventName, data);
      console.debug(`Emitted event '${eventName}' to room '${room}'`);
    } catch (error) {
      console.error(`Failed to emit event '${eventName}' to room '${room}': ${error.message}`);
    }
  }

  /**
   * Notifies admin users about a new MFA code for an agent.
   * @param {string} agentId - The unique agent ID.
   * @param {string} mfaCode - The generated MFA code.
   * @param {Object} [positionInfo={}] - Additional information (e.g., computer lab).
   * @param {number} [positionInfo.roomId] - Room ID where the agent is located.
   * @param {number} [positionInfo.posX] - X position in the room grid.
   * @param {number} [positionInfo.posY] - Y position in the room grid.
   * @param {string} [positionInfo.roomName] - Name of the room where the agent is located.
   */
  notifyAdminsNewMfa(mfaCode, positionInfo = {}) {
    const eventData = {
      mfaCode,
      positionInfo,
      timestamp: new Date(),
    };
    this._emitToRoom(ROOM_PREFIXES.ADMIN, EVENTS.ADMIN_NEW_AGENT_MFA, eventData);
  }

  /**
   * Notifies admin users when an agent has been successfully registered.
   * @param {number} computerId - The computer ID in the database.
   * @param {Object} positionInfo - Additional information about the agent's position.
   * @param {number} positionInfo.roomId - Room ID where the agent is located.
   * @param {number} positionInfo.posX - X position in the room grid.
   * @param {number} positionInfo.posY - Y position in the room grid.
   */
  notifyAdminsAgentRegistered(computerId, positionInfo) {
     const eventData = {
      computerId,
      positionInfo,
      timestamp: new Date(),
    };
    this._emitToRoom(ROOM_PREFIXES.ADMIN, EVENTS.ADMIN_AGENT_REGISTERED, eventData);
    console.info(`Agent registration notification sent for agent ${agentId} (Computer ID: ${computerId})`);
  }

  /**
   * Updates the realtime cache for a computer (uses Map).
   * @param {number} computerId - The computer ID.
   * @param {object} data - The status data to update.
   * @param {number} [data.cpuUsage] - CPU usage percentage.
   * @param {number} [data.ramUsage] - RAM usage percentage.
   * @param {number} [data.diskUsage] - Disk usage percentage.
   */
  updateRealtimeCache(computerId, data) {
    if (!computerId || typeof computerId !== 'number') {
        console.warn('Invalid computerId provided for updateRealtimeCache');
        return;
    }

    const existingData = this.agentRealtimeStatus.get(computerId) || {};

    const newData = {
      ...existingData,
      ...data,
      lastUpdated: new Date(),
    };

    this.agentRealtimeStatus.set(computerId, newData);

    console.debug(`Realtime cache (Map) updated for computer ${computerId}`);
  }

  /**
   * Checks if an agent is currently connected (based on socket presence in room).
   * @param {number} computerId - The computer ID.
   * @returns {boolean} True if the agent is connected.
   */
  isAgentConnected(computerId) {
    try {
      const room = this.io.sockets.adapter.rooms.get(ROOM_PREFIXES.AGENT(computerId));
      return !!room && room.size > 0;
    } catch (error) {
      console.error(`Error checking online status for computer ${computerId}: ${error.message}`);
      return false;
    }
  }

  /**
   * Returns the number of connected agents.
   * @returns {number} The number of connected agents.
   */
  numberOfConnectedAgents() {
    return this.agentRealtimeStatus.size;
  }

  /**
   * Retrieves the realtime status and system information for an agent from the cache (Map).
   * @param {number} computerId - The computer ID.
   * @returns {object | null} A copy of the agent's status data, or null if not found.
   * Contains fields such as:
   * - status: "online" | "offline" - Current connection status.
   * - cpuUsage: number - CPU usage percentage.
   * - ramUsage: number - RAM usage percentage.
   * - diskUsage: number - Disk usage percentage.
   * - lastUpdated: Date - Timestamp of the last status update.
   */
  getAgentRealtimeStatus(computerId) {
    if (!computerId) return null;
    const status = this.agentRealtimeStatus.get(computerId);
    return status ? { ...status } : null;
  }

  /**
   * Handles an agent's disconnection. Removes status from Map after confirming offline state.
   * @param {import("socket.io").Socket} socket - The disconnected socket instance.
   * @param {Object} socket.data - Socket metadata.
   * @param {number} socket.data.computerId - The computer ID associated with this socket.
   */
  async handleAgentDisconnect(socket) {
    const computerId = socket.data?.computerId;

    if (!computerId) {
      console.debug(`Socket ${socket.id} disconnected without associated computerId.`);
      return;
    }

    console.info(`Handling disconnect for agent socket ${socket.id} (Computer ID: ${computerId})`);

    try {
      setTimeout(async () => {
        if (!this.isAgentConnected(computerId)) {
          console.info(`Computer ${computerId} confirmed offline after delay.`);

          const offlineStatusData = {
              status: 'offline',
              cpuUsage: 0,
              ramUsage: 0,
              diskUsage: 0,
              timestamp: new Date(),
          };

          this._emitToRoom(
              ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId),
              EVENTS.COMPUTER_STATUS_UPDATED,
              offlineStatusData
          );
          console.info(`Broadcasted final 'offline' status for computer ${computerId}.`);

          const deleted = this.agentRealtimeStatus.delete(computerId);
          if (deleted) {
            console.info(`Removed realtime status for computer ${computerId} from Map.`);
          } else {
            console.warn(`Attempted to remove status for computer ${computerId}, but it was not found in the Map.`);
          }

        } else {
           console.info(`Computer ${computerId} still has other connections active. Not marking as offline or removing status yet.`);
        }
      }, AGENT_OFFLINE_CHECK_DELAY_MS);

    } catch (error) {
      console.error(`Error handling agent disconnect for computer ${computerId} (Socket ID: ${socket.id}): ${error.message}`, error.stack);
    }
  }

  /**
   * Broadcasts a computer status update to subscribers (retrieved from Map).
   * @param {number} computerId - The computer ID.
   */
  async broadcastStatusUpdate(computerId) {
     if (!computerId) {
        console.warn('broadcastStatusUpdate called with invalid computerId');
        return;
    }
    try {
      const statusData = this.getAgentRealtimeStatus(computerId);

      if (!statusData) {
          console.warn(`No status data found in Map for computer ${computerId}. Skipping broadcast.`);
          return;
      }

      const eventData = {
        computerId,
        status: "online",
        cpuUsage: statusData.cpuUsage ?? 0,
        ramUsage: statusData.ramUsage ?? 0,
        diskUsage: statusData.diskUsage ?? 0,
        timestamp: statusData.lastUpdated || new Date(),
      };

      this._emitToRoom(ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId), EVENTS.COMPUTER_STATUS_UPDATED, eventData);

    } catch (error) {
      console.error(`Failed during status broadcast preparation for computer ${computerId}: ${error.message}`, error.stack);
    }
  }

  /**
   * Stores a pending command.
   * @param {string} commandId - The command ID.
   * @param {number} userId - The ID of the user who initiated the command.
   * @param {number} computerId - The target computer ID.
   */
  storePendingCommand(commandId, userId, computerId) {
     if (!commandId || !userId || !computerId) {
        console.warn('storePendingCommand called with invalid parameters', { commandId, userId, computerId });
        return;
    }

    if (this.pendingCommands.has(commandId)) {
        const existingCommand = this.pendingCommands.get(commandId);
        clearTimeout(existingCommand.timeoutId);
        console.warn(`Command ${commandId} already existed in pending commands. Overwriting and clearing old timeout.`);
    }

    const timeoutId = setTimeout(() => {
      if (this.pendingCommands.has(commandId)) {
        console.warn(`Command ${commandId} timed out and removed from pending commands.`);
        this.pendingCommands.delete(commandId);
      }
    }, PENDING_COMMAND_TIMEOUT_MS);

    this.pendingCommands.set(commandId, {
      userId,
      computerId,
      timestamp: new Date(),
      timeoutId,
    });

    console.debug(`Command ${commandId} stored as pending for computer ${computerId} by user ${userId}`);
  }

  /**
   * Sends a command to a specific agent.
   * @param {number} computerId - The target computer ID.
   * @param {string} command - The command to execute.
   * @param {string} commandId - The command ID.
   * @param {string} [commandType='console'] - Type of command (console, script, etc).
   * @returns {boolean} True if the command was successfully emitted to at least one agent socket.
   */
  sendCommandToAgent(computerId, command, commandId, commandType = 'console') {
    if (!computerId || !command || !commandId) {
        console.warn('sendCommandToAgent called with invalid parameters', { computerId, commandId });
        return false;
    }
    try {

      const agentRoom = ROOM_PREFIXES.AGENT(computerId);

      if (!this.isAgentConnected(computerId)) {
        console.warn(`Cannot send command ${commandId} to computer ${computerId}: Agent is not connected.`);
        return false;
      }

      this.io.to(agentRoom).emit(EVENTS.COMMAND_EXECUTE, { commandId, command, commandType });

      console.info(`Command ${commandId} (type: ${commandType}) sent to agent room ${agentRoom} for computer ${computerId}`);
      return true;
    } catch (error) {
      console.error(`Error sending command ${commandId} to computer ${computerId}: ${error.message}`, error.stack);
      return false;
    }
  }

  /**
   * Notifies the initiating user about command completion.
   * @param {string} commandId - The command ID.
   * @param {object} result - The command execution result from the agent.
   * @param {string} result.type - Command type (console, script, etc.)
   * @param {boolean} result.success - Whether command was successful
   * @param {string} result.result - Command result object
   */
  notifyCommandCompletion(commandId, result) {
     if (!commandId || !result) {
        console.warn('notifyCommandCompletion called with invalid parameters', { commandId });
        return;
    }
    try {
      const pendingCommand = this.pendingCommands.get(commandId);

      if (!pendingCommand) {
        console.warn(`Received completion for unknown or already processed command ${commandId}. Ignoring.`);
        return;
      }

      const { userId, computerId, timeoutId } = pendingCommand;

      clearTimeout(timeoutId);

      this.pendingCommands.delete(commandId);

      // Format the result for frontend clients
      const formattedResult = {
        commandId,
        computerId,
        type: result.type || 'console',
        success: result.success === true,
        result: result.result || '',
        timestamp: new Date(),
      };

      this._emitToRoom(ROOM_PREFIXES.USER(userId), EVENTS.COMMAND_COMPLETED, formattedResult);

      console.debug(`Command ${commandId} completion notified to user ${userId}`);
    } catch (error) {
      console.error(`Error processing command completion notification for ${commandId}: ${error.message}`, error.stack);
    }
  }

}

module.exports = new WebSocketService();
module.exports.EVENTS = EVENTS;
module.exports.ROOM_PREFIXES = ROOM_PREFIXES;
