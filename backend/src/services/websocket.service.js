/**
 * Service for WebSocket operations, optimized for clarity and maintainability.
 * Standardized based on agent's communication patterns.
 * Uses Map for realtime status and removes status on final disconnect.
 */
const logger = require('../utils/logger');

const ROOM_PREFIXES = {
  ADMIN: 'admin_room',
  AGENT: (id) => `agent_${id}`,
  COMPUTER_SUBSCRIBERS: (id) => `computer_${id}`,
  USER: (id) => `user_${id}`,
};

const EVENTS = {
  // Admin events
  ADMIN_NEW_AGENT_MFA: 'admin:new_agent_mfa',
  ADMIN_AGENT_REGISTERED: 'admin:agent_registered',
  
  // Command events
  COMMAND_EXECUTE: 'command:execute',
  COMMAND_COMPLETED: 'command:completed',
  
  // Computer status events
  COMPUTER_STATUS_UPDATED: 'computer:status_updated',
  
  // Agent notification events
  NEW_VERSION_AVAILABLE: 'agent:new_version_available',
};

const PENDING_COMMAND_TIMEOUT_MS = 5 * 60 * 1000;
const AGENT_OFFLINE_CHECK_DELAY_MS = 1500;

class WebSocketService {

  constructor() {
    this.io = null;
    this.agentRealtimeStatus = new Map();
    this.pendingCommands = new Map();

    logger.info('WebSocketService initialized (using Map for agent status, room-based connection check)');
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
    logger.info(`MFA code notification sent to admin room for position: ${positionInfo.roomName || 'N/A'} (${positionInfo.posX},${positionInfo.posY})`);
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
    logger.info(`Agent registration notification for Computer ID: ${computerId} with position info sent to admin room.`);
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
   * Checks if an agent is currently connected (based on socket presence in its dedicated room).
   * @param {number} computerId - The computer ID.
   * @returns {boolean} True if the agent has at least one active socket connection.
   */
  isAgentConnected(computerId) {
    if (!this.io || !computerId) return false;
    try {
      const room = this.io.sockets.adapter.rooms.get(ROOM_PREFIXES.AGENT(computerId));
      return !!room && room.size > 0;
    } catch (error) {
      logger.error(`Error checking online status for computer ${computerId}: ${error.message}`);
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
   * Handles an agent's socket disconnection.
   * Waits briefly, then checks if *any* connection for that agent remains.
   * If not, broadcasts 'offline' status and removes the agent's status from the cache.
   * @param {import("socket.io").Socket} socket - The disconnected socket instance.
   * @param {Object} socket.data - Socket metadata.
   * @param {number} socket.data.computerId - The computer ID associated with this socket.
   */
  async handleAgentDisconnect(socket) {
    const computerId = socket.data?.computerId;

    if (!computerId) {
      logger.debug(`Socket ${socket.id} disconnected without an associated computerId.`);
      return;
    }

    logger.info(`Handling disconnect for agent socket ${socket.id} (Computer ID: ${computerId})`);

    setTimeout(async () => {
      try {
        if (!this.isAgentConnected(computerId)) {
          logger.info(`Computer ${computerId} confirmed offline after delay.`);

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
          logger.info(`Broadcasted final 'offline' status for computer ${computerId}.`);

          const deleted = this.agentRealtimeStatus.delete(computerId);
          if (deleted) {
            logger.info(`Removed realtime status for computer ${computerId} from Map.`);
          } else {
            logger.warn(`Attempted to remove status for computer ${computerId}, but it was not found in the Map (might have been removed already).`);
          }

        } else {
           logger.info(`Computer ${computerId} still has other connections active. Not marking as offline or removing status yet.`);
        }
      } catch (error) {
         logger.error(`Error during delayed offline check for computer ${computerId} (Socket ID: ${socket.id}): ${error.message}`, { stack: error.stack });
      }
    }, AGENT_OFFLINE_CHECK_DELAY_MS);
  }

  /**
   * Broadcasts a computer status update to subscribers (retrieved from Map).
   * Assumes the agent is online when this is called based on received data.
   * @param {number} computerId - The computer ID.
   */
  async broadcastStatusUpdate(computerId) {
     if (!computerId || typeof computerId !== 'number') {
        logger.warn('broadcastStatusUpdate called with invalid computerId');
        return;
    }
    try {
      const statusData = this.getAgentRealtimeStatus(computerId);

      if (!statusData) {
          logger.warn(`No status data found in Map for computer ${computerId} during broadcast attempt. Skipping.`);
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
      logger.error(`Failed during status broadcast preparation for computer ${computerId}: ${error.message}`, { stack: error.stack });
    }
  }

  /**
   * Stores a pending command and sets a timeout for cleanup.
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
        logger.warn(`Command ${commandId} timed out after ${PENDING_COMMAND_TIMEOUT_MS}ms and removed from pending commands.`);
        this.pendingCommands.delete(commandId);
      }
    }, PENDING_COMMAND_TIMEOUT_MS);

    this.pendingCommands.set(commandId, {
      userId,
      computerId,
      timestamp: new Date(),
      timeoutId,
    });

    logger.debug(`Command ${commandId} stored as pending for computer ${computerId} by user ${userId}. Timeout set.`);
  }

  /**
   * Sends a command to a specific agent by emitting to its dedicated room.
   * @param {number} computerId - The target computer ID.
   * @param {string} command - The command payload/string to execute.
   * @param {string} commandId - The unique command ID for tracking.
   * @param {string} [commandType='console'] - Type of command (e.g., console, script, power).
   * @returns {boolean} True if the command was successfully emitted to the agent's room.
   */
  sendCommandToAgent(computerId, command, commandId, commandType = 'console') {
    if (!computerId || !command || !commandId) {
        logger.warn('sendCommandToAgent called with invalid parameters', { computerId, commandId, commandExists: !!command });
        return false;
    }

    if (typeof command !== 'string' || command.length > 2000) {
        logger.warn(`Invalid command format or length exceeds 2000 characters for command ID ${commandId}`);
        return false;
    }

    try {
      const agentRoom = ROOM_PREFIXES.AGENT(computerId);

      if (!this.isAgentConnected(computerId)) {
        logger.warn(`Cannot send command ${commandId} to computer ${computerId}: Agent is not connected (no sockets found in room ${agentRoom}).`);
        return false;
      }

      // Emit event with format matching the API documentation
      this.io.to(agentRoom).emit(EVENTS.COMMAND_EXECUTE, { 
        command,
        commandId, 
        commandType
      });

      logger.info(`Command ${commandId} (type: ${commandType}) sent to agent room ${agentRoom} for computer ${computerId}`);
      return true;

    } catch (error) {
      logger.error(`Error sending command ${commandId} to computer ${computerId}: ${error.message}`, { stack: error.stack });
      return false;
    }
  }

  /**
   * Notifies the initiating user about command completion or failure.
   * Clears the pending command entry.
   * @param {string} commandId - The command ID.
   * @param {object} result - The command execution result received from the agent.
   * @param {string} [result.type] - Command type (console, script, etc.) - Passed through from agent.
   * @param {boolean} result.success - Whether the agent reported success.
   * @param {any} result.result - The actual output or result data from the agent.
   */
  notifyCommandCompletion(commandId, result) {
     if (!commandId || !result) {
        logger.warn('notifyCommandCompletion called with invalid parameters', { commandId, resultExists: !!result });
        return;
    }
    try {
      const pendingCommand = this.pendingCommands.get(commandId);

      if (!pendingCommand) {
        logger.warn(`Received completion for unknown or already timed-out/processed command ${commandId}. Ignoring.`);
        return;
      }

      const { userId, computerId, timeoutId } = pendingCommand;

      clearTimeout(timeoutId);

      this.pendingCommands.delete(commandId);

      const formattedResult = {
        commandId,
        computerId,
        type: result.type || 'unknown',
        success: result.success === true,
        result: result.result,
        timestamp: new Date(),
      };

      this._emitToRoom(ROOM_PREFIXES.USER(userId), EVENTS.COMMAND_COMPLETED, formattedResult);

      logger.debug(`Command ${commandId} completion notified to user ${userId}`);

    } catch (error) {
      logger.error(`Error processing command completion notification for ${commandId}: ${error.message}`, { stack: error.stack });
    }
  }


   /**
   * Joins an authenticated user's socket to their dedicated room for direct notifications.
   * @param {import("socket.io").Socket} socket - The user's socket instance.
   * @param {number} userId - The authenticated user's ID.
   */
  joinUserRoom(socket, userId) {
    if (!socket || !userId) {
      logger.error('joinUserRoom called with invalid socket or userId');
      return;
    }
    try {
      const roomName = ROOM_PREFIXES.USER(userId);
      socket.join(roomName);
      socket.data.userId = userId;
      logger.info(`User socket ${socket.id} (User ID: ${userId}) joined user room: ${roomName}`);
    } catch (error) {
      logger.error(`Error joining user room for User ID ${userId} (Socket ${socket.id}): ${error.message}`, { stack: error.stack });
    }
  }

   /**
   * Joins an authenticated admin's socket to the admin room for admin-specific events.
   * @param {import("socket.io").Socket} socket - The admin's socket instance.
   */
  joinAdminRoom(socket) {
    if (!socket) {
      logger.error('joinAdminRoom called with invalid socket');
      return;
    }
    try {
      const roomName = ROOM_PREFIXES.ADMIN;
      socket.join(roomName);
      logger.info(`Admin socket ${socket.id} joined admin room: ${roomName}`);
    } catch (error) {
      logger.error(`Error joining admin room for Socket ${socket.id}: ${error.message}`, { stack: error.stack });
    }
  }


  /**
   * Joins computer subscriber room for a specific computer.
   * This is used for clients (e.g., admin UI) to receive updates about the computer's status.
   * @param {import("socket.io").Socket} socket - The socket instance.
   * @param {number} computerId - The computer ID.
   */
  joinComputerRoom(socket, computerId) {
    if (!socket || !computerId) {
      logger.error('joinComputerRoom called with invalid socket or computerId');
      return;
    }
    try {
      const roomName = ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId);
      socket.join(roomName);
      logger.info(`Socket ${socket.id} joined computer subscriber room: ${roomName}`);
    } catch (error) {
      logger.error(`Error joining computer subscriber room for Computer ID ${computerId} (Socket ${socket.id}): ${error.message}`, { stack: error.stack });
    }
  }

  /**
   * Joins agent room for a specific computer.
   * @param {import("socket.io").Socket} socket - The socket instance.
   * @param {number} computerId - The computer ID.
   */
  joinAgentRoom(socket, computerId) {
    if (!socket || !computerId) {
      logger.error('joinAgentRoom called with invalid socket or computerId');
      return;
    }
    try {
      const roomName = ROOM_PREFIXES.AGENT(computerId);
      socket.join(roomName);
      logger.info(`Socket ${socket.id} joined agent room for computer ${computerId}: ${roomName}`);
    } catch (error) {
      logger.error(`Error joining agent room for Computer ID ${computerId} (Socket ${socket.id}): ${error.message}`, { stack: error.stack });
    }
  }

  /**
   * Notifies all connected agents about a new version being available
   * @param {Object} [versionInfo] - Information about the new agent version
   * @param {string} [versionInfo.version] - The version string (e.g., "1.2.0")
   * @param {string} [versionInfo.download_url] - URL to download the update package
   * @param {string} [versionInfo.checksum_sha256] - SHA-256 checksum of the update package
   * @param {string} [versionInfo.notes] - Release notes
   * @returns {Promise<number>} Number of connected agents that were notified
   */
  /**
   * Notifies all connected agents about a new version being available
   * @param {Object} [versionInfo] - Information about the new agent version
   * @param {string} [versionInfo.version] - The version string (e.g., "1.2.0")
   * @param {string} [versionInfo.download_url] - URL to download the update package
   * @param {string} [versionInfo.checksum_sha256] - SHA-256 checksum of the update package
   * @param {string} [versionInfo.notes] - Release notes
   * @returns {Promise<void>}
   */
  async notifyAgentsOfNewVersion(versionInfo = {}) {
    try {
      // Validate required fields
      if (!versionInfo.version || !versionInfo.download_url || !versionInfo.checksum_sha256) {
        logger.warn('Incomplete version information provided for agent notification', { versionInfo });
        return;
      }
      
      // Format the event data according to the API documentation
      const eventData = {
        status: "success",
        update_available: true,
        version: versionInfo.version,
        download_url: versionInfo.download_url,
        checksum_sha256: versionInfo.checksum_sha256,
        notes: versionInfo.notes || ""
      };

      // Get all agent rooms
      const rooms = this.io.sockets.adapter.rooms;
      let notifiedRooms = 0;

      // For each room, check if it's an agent room
      for (const [roomName] of rooms.entries()) {
        if (roomName.startsWith('agent_')) {
          this._emitToRoom(roomName, EVENTS.NEW_VERSION_AVAILABLE, eventData);
          logger.debug(`Notified room ${roomName} about new agent version: ${eventData.version}`);
          notifiedRooms++;
        }
      }

      logger.info(`Notified ${notifiedRooms} agent rooms about new agent version: ${eventData.version}`);
    } catch (error) {
      logger.error('Error notifying agents of new version:', { 
        error: error.message, 
        stack: error.stack,
        version: versionInfo?.version
      });
    }
  }
}

module.exports = new WebSocketService();
module.exports.EVENTS = EVENTS;
module.exports.ROOM_PREFIXES = ROOM_PREFIXES;
