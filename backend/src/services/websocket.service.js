/**
 * Service for WebSocket operations, optimized for clarity and maintainability.
 * Standardized based on agent's communication patterns.
 * Uses Map for realtime status and removes status on final disconnect.
 */
const logger = require('../utils/logger');

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

    logger.info('WebSocketService initialized (using Map for agent status)');
  }

  /**
   * Sets the Socket.IO server instance.
   * @param {import("socket.io").Server} io - The Socket.IO Server instance.
   */
  setIo(io) {
    if (!io) {
      logger.error('Attempted to set null or undefined Socket.IO instance');
      return;
    }
    this.io = io;
    logger.info('Socket.IO instance has been set in WebSocketService');
  }

  /**
   * Ensures the Socket.IO instance is initialized.
   * @private
   * @throws {Error} If IO is not initialized.
   */
  _ensureIoInitialized() {
    if (!this.io) {
      const errorMsg = 'WebSocket IO not initialized. Call setIo() first.';
      logger.error(errorMsg);
      throw new Error(errorMsg);
    }
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
        logger.error(errorMsg);
        throw new Error(errorMsg);
      }
      this.io.to(room).emit(eventName, data);
      logger.debug(`Emitted event '${eventName}' to room '${room}'`);
    } catch (error) {
      logger.error(`Failed to emit event '${eventName}' to room '${room}': ${error.message}`);
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
   * @param {string} agentId - The unique agent ID.
   * @param {number} computerId - The computer ID in the database.
   */
  notifyAdminsAgentRegistered(agentId, computerId) {
     const eventData = {
      unique_agent_id: agentId,
      computerId,
      timestamp: new Date(),
    };
    this._emitToRoom(ROOM_PREFIXES.ADMIN, EVENTS.ADMIN_AGENT_REGISTERED, eventData);
    logger.info(`Agent registration notification sent for agent ${agentId} (Computer ID: ${computerId})`);
  }

  /**
   * Updates the realtime cache for a computer (uses Map).
   * @param {number} computerId - The computer ID.
   * @param {object} data - The status data to update.
   * @param {string} [data.status] - Computer status (online/offline).
   * @param {number} [data.cpuUsage] - CPU usage percentage.
   * @param {number} [data.ramUsage] - RAM usage percentage.
   * @param {number} [data.diskUsage] - Disk usage percentage.
   * @param {string} [data.osInfo] - Operating system information.
   * @param {string} [data.error] - Any error message from the agent.
   */
  updateRealtimeCache(computerId, data) {
    if (!computerId || typeof computerId !== 'number') {
        logger.warn('Invalid computerId provided for updateRealtimeCache');
        return;
    }

    const existingData = this.agentRealtimeStatus.get(computerId) || {};

    const newData = {
      ...existingData,
      ...data,
      lastUpdated: new Date(),
    };

    this.agentRealtimeStatus.set(computerId, newData);

    logger.debug(`Realtime cache (Map) updated for computer ${computerId}`);
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
      logger.error(`Error checking online status for computer ${computerId}: ${error.message}`);
      return false;
    }
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
   * - error: string - Any error message, if present.
   * - osInfo: object - Information about the operating system.
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
      logger.debug(`Socket ${socket.id} disconnected without associated computerId.`);
      return;
    }

    logger.info(`Handling disconnect for agent socket ${socket.id} (Computer ID: ${computerId})`);

    try {
      setTimeout(async () => {
        if (!this.isAgentConnected(computerId)) {
          logger.info(`Computer ${computerId} confirmed offline after delay.`);

          const offlineStatusData = {
              computerId,
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
          logger.info(`Broadcasted final 'offline' status for computer ${computerId}.`);

          const deleted = this.agentRealtimeStatus.delete(computerId);
          if (deleted) {
            logger.info(`Removed realtime status for computer ${computerId} from Map.`);
          } else {
            logger.warn(`Attempted to remove status for computer ${computerId}, but it was not found in the Map.`);
          }

        } else {
           logger.info(`Computer ${computerId} still has other connections active. Not marking as offline or removing status yet.`);
        }
      }, AGENT_OFFLINE_CHECK_DELAY_MS);

    } catch (error) {
      logger.error(`Error handling agent disconnect for computer ${computerId} (Socket ID: ${socket.id}): ${error.message}`, error.stack);
    }
  }

  /**
   * Broadcasts a computer status update to subscribers (retrieved from Map).
   * @param {number} computerId - The computer ID.
   */
  async broadcastStatusUpdate(computerId) {
     if (!computerId) {
        logger.warn('broadcastStatusUpdate called with invalid computerId');
        return;
    }
    try {
      const statusData = this.getAgentRealtimeStatus(computerId);

      if (!statusData) {
          logger.warn(`No status data found in Map for computer ${computerId}. Skipping broadcast.`);
          return;
      }

      const eventData = {
        computerId,
        status: statusData.status,
        cpuUsage: statusData.cpuUsage ?? 0,
        ramUsage: statusData.ramUsage ?? 0,
        diskUsage: statusData.diskUsage ?? 0,
        timestamp: statusData.lastUpdated || new Date(),
      };

      this._emitToRoom(ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId), EVENTS.COMPUTER_STATUS_UPDATED, eventData);

    } catch (error) {
      logger.error(`Failed during status broadcast preparation for computer ${computerId}: ${error.message}`, error.stack);
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
        logger.warn('storePendingCommand called with invalid parameters', { commandId, userId, computerId });
        return;
    }

    if (this.pendingCommands.has(commandId)) {
        const existingCommand = this.pendingCommands.get(commandId);
        clearTimeout(existingCommand.timeoutId);
        logger.warn(`Command ${commandId} already existed in pending commands. Overwriting and clearing old timeout.`);
    }

    const timeoutId = setTimeout(() => {
      if (this.pendingCommands.has(commandId)) {
        logger.warn(`Command ${commandId} timed out and removed from pending commands.`);
        this.pendingCommands.delete(commandId);
      }
    }, PENDING_COMMAND_TIMEOUT_MS);

    this.pendingCommands.set(commandId, {
      userId,
      computerId,
      timestamp: new Date(),
      timeoutId,
    });

    logger.debug(`Command ${commandId} stored as pending for computer ${computerId} by user ${userId}`);
  }

  /**
   * Sends a command to a specific agent.
   * @param {number} computerId - The target computer ID.
   * @param {string} command - The command to execute.
   * @param {string} commandId - The command ID.
   * @returns {boolean} True if the command was successfully emitted to at least one agent socket.
   */
  sendCommandToAgent(computerId, command, commandId) {
    if (!computerId || !command || !commandId) {
        logger.warn('sendCommandToAgent called with invalid parameters', { computerId, commandId });
        return false;
    }
    try {

      const agentRoom = ROOM_PREFIXES.AGENT(computerId);

      if (!this.isAgentConnected(computerId)) {
        logger.warn(`Cannot send command ${commandId} to computer ${computerId}: Agent is not connected.`);
        return false;
      }

      this.io.to(agentRoom).emit(EVENTS.COMMAND_EXECUTE, { commandId, command });

      logger.info(`Command ${commandId} sent to agent room ${agentRoom} for computer ${computerId}`);
      return true;
    } catch (error) {
      logger.error(`Error sending command ${commandId} to computer ${computerId}: ${error.message}`, error.stack);
      return false;
    }
  }

  /**
   * Notifies the initiating user about command completion.
   * @param {string} commandId - The command ID.
   * @param {object} result - The command execution result from the agent.
   * @param {string} result.stdout - Standard output from the command.
   * @param {string} result.stderr - Standard error from the command.
   * @param {number} result.exitCode - Exit code from the command.
   */
  notifyCommandCompletion(commandId, result) {
     if (!commandId || !result) {
        logger.warn('notifyCommandCompletion called with invalid parameters', { commandId });
        return;
    }
    try {
      const pendingCommand = this.pendingCommands.get(commandId);

      if (!pendingCommand) {
        logger.warn(`Received completion for unknown or already processed command ${commandId}. Ignoring.`);
        return;
      }

      const { userId, computerId, timeoutId } = pendingCommand;

      clearTimeout(timeoutId);

      this.pendingCommands.delete(commandId);

      const formattedResult = {
        commandId,
        computerId,
        stdout: result.stdout ?? '',
        stderr: result.stderr ?? '',
        exitCode: typeof result.exitCode === 'number' ? result.exitCode : -1,
        timestamp: new Date(),
      };

      this._emitToRoom(ROOM_PREFIXES.USER(userId), EVENTS.COMMAND_COMPLETED, formattedResult);

      logger.debug(`Command ${commandId} completion notified to user ${userId}`);
    } catch (error) {
      logger.error(`Error processing command completion notification for ${commandId}: ${error.message}`, error.stack);
    }
  }

}

module.exports = new WebSocketService();
module.exports.EVENTS = EVENTS;
module.exports.ROOM_PREFIXES = ROOM_PREFIXES;
