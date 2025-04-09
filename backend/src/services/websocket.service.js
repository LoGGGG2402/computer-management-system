/**
 * Service for WebSocket operations
 */
class WebSocketService {
  constructor() {
    this.io = null;
    // Map to store agent socket connections (computerId -> socketId)
    this.agentCommandSockets = new Map();
    // Set to store admin socket IDs
    this.adminSockets = new Set();
    // Object to store agent realtime status
    this.agentRealtimeStatus = {};
    // Map to store pending commands (commandId -> {userId, computerId, timestamp})
    this.pendingCommands = new Map();
  }

  /**
   * Set the Socket.IO instance
   * @param {Object} io - The Socket.IO instance
   */
  setIo(io) {
    this.io = io;
  }

  /**
   * Register an agent socket
   * @param {number} computerId - The computer ID
   * @param {string} socketId - The socket ID
   */
  registerAgentSocket(computerId, socketId) {
    this.agentCommandSockets.set(computerId, socketId);
    console.log(`Agent socket registered for computer ${computerId}: ${socketId}`);
  }

  /**
   * Unregister an agent socket
   * @param {number} computerId - The computer ID
   */
  unregisterAgentSocket(computerId) {
    this.agentCommandSockets.delete(computerId);
    console.log(`Agent socket unregistered for computer ${computerId}`);
  }

  /**
   * Find a computer ID by its socket ID
   * @param {string} socketId - The socket ID
   * @returns {number|null} The computer ID or null if not found
   */
  findComputerIdBySocketId(socketId) {
    for (const [computerId, sid] of this.agentCommandSockets.entries()) {
      if (sid === socketId) {
        return computerId;
      }
    }
    return null;
  }

  /**
   * Get the socket ID for a computer
   * @param {number} computerId - The computer ID
   * @returns {string|undefined} The socket ID or undefined if not found
   */
  getAgentSocketId(computerId) {
    return this.agentCommandSockets.get(computerId);
  }

  /**
   * Register an admin socket
   * @param {string} socketId - The socket ID
   */
  registerAdminSocket(socketId) {
    this.adminSockets.add(socketId);
    console.log(`Admin socket registered: ${socketId}`);
  }

  /**
   * Unregister an admin socket
   * @param {string} socketId - The socket ID
   */
  unregisterAdminSocket(socketId) {
    this.adminSockets.delete(socketId);
    console.log(`Admin socket unregistered: ${socketId}`);
  }

  /**
   * Notify admin users about a new MFA code for an agent
   * @param {string} agentId - The unique agent ID
   * @param {string} mfaCode - The generated MFA code
   * @param {Object} positionInfo - Additional room information
   */
  notifyAdminsNewMfa(agentId, mfaCode, positionInfo = {}) {
    if (!this.io) {
      console.log('WebSocket IO not initialized');
      return;
    }

    console.log(`[WebSocket] Sending MFA notification to admins:`, {
      agentId,
      mfaCode,
      positionInfo,
      adminSockets: Array.from(this.adminSockets)
    });

    // Emit to each admin socket individually
    this.adminSockets.forEach(socketId => {
      console.log(`[WebSocket] Emitting MFA to socket ${socketId}`);
      this.io.to(socketId).emit('admin:new_agent_mfa', {
        unique_agent_id: agentId,
        mfaCode,
        positionInfo,
        timestamp: new Date()
      });
    });
  }

  /**
   * Notify admin users when an agent has been successfully registered
   * @param {string} agentId - The unique agent ID
   * @param {number} computerId - The computer ID in the database
   */
  notifyAdminsAgentRegistered(agentId, computerId) {
    if (!this.io) return;

    // Emit to each admin socket individually
    this.adminSockets.forEach(socketId => {
      this.io.to(socketId).emit('admin:agent_registered', {
        unique_agent_id: agentId,
        computerId,
        timestamp: new Date()
      });
    });
  }

  /**
   * Update the realtime cache for a computer
   * @param {number} computerId - The computer ID
   * @param {Object} data - The status data to update
   */
  updateRealtimeCache(computerId, data) {
    if (!this.agentRealtimeStatus[computerId]) {
      this.agentRealtimeStatus[computerId] = {};
    }
    
    // Update cache with new data
    this.agentRealtimeStatus[computerId] = {
      ...this.agentRealtimeStatus[computerId],
      ...data,
      lastUpdated: new Date()
    };
  }

  /**
   * Check if an agent is currently online
   * @param {number} computerId - The computer ID
   * @returns {boolean} True if the agent is online
   */
  getAgentOnlineStatus(computerId) {
    return this.agentCommandSockets.has(computerId);
  }

  /**
   * Update the agent's online status and broadcast to room
   * @param {number} computerId - The computer ID
   */
  async updateAndBroadcastOnlineStatus(computerId) {
    try {
      // Import computerService to avoid circular dependencies
      const computerService = require('./computer.service');
      
      // Update the last seen timestamp in database
      await computerService.updateLastSeen(computerId);
      
      // Update cache with online status
      this.updateRealtimeCache(computerId, { 
        status: 'online',
        lastSeen: new Date()
      });
      
      // Get the latest status from cache
      await this.broadcastStatusUpdate(computerId);
      
      console.log(`Updated online status for computer ${computerId}`);
    } catch (error) {
      console.error(`Error updating online status for computer ${computerId}:`, error);
    }
  }

  /**
   * Handle agent disconnection
   * @param {string} socketId - The socket ID
   */
  async handleAgentDisconnect(socketId) {
    try {
      const computerId = this.findComputerIdBySocketId(socketId);
      
      if (computerId) {
        console.log(`Handling disconnect for agent with computer ID ${computerId}`);
        
        // Unregister the socket
        this.unregisterAgentSocket(computerId);
        
        // Update cache with offline status
        this.updateRealtimeCache(computerId, { 
          status: 'offline',
          lastDisconnected: new Date()
        });
        
        // Broadcast the status update to the room
        await this.broadcastStatusUpdate(computerId);
        
        console.log(`Agent for computer ${computerId} is now marked as offline`);
      }
    } catch (error) {
      console.error(`Error handling agent disconnect for socket ${socketId}:`, error);
    }
  }

  /**
   * Broadcast a computer status update to the appropriate room
   * @param {number} computerId - The computer ID
   */
  async broadcastStatusUpdate(computerId) {
    try {
      if (!this.io) return;
      
      // Get the status data from cache
      const statusData = this.agentRealtimeStatus[computerId] || { status: 'offline' };
      
      // Get the room ID for this computer (implement this method or inject through a service)
      const roomId = await this.getComputerRoomId(computerId);
      
      if (roomId) {
        // Broadcast to the room
        this.io.to(`room_${roomId}`).emit('computer:status_updated', {
          computerId,
          status: statusData.status,
          cpuUsage: statusData.cpuUsage,
          ramUsage: statusData.ramUsage,
          diskUsage: statusData.diskUsage,
          timestamp: new Date()
        });
        
        console.log(`Broadcasted status update for computer ${computerId} to room ${roomId}`);
      }
    } catch (error) {
      console.error(`Error broadcasting status update for computer ${computerId}:`, error);
    }
  }

  /**
   * Store a pending command
   * @param {string} commandId - The command ID
   * @param {number} userId - The user ID who initiated the command
   * @param {number} computerId - The target computer ID
   */
  storePendingCommand(commandId, userId, computerId) {
    this.pendingCommands.set(commandId, {
      userId,
      computerId,
      timestamp: new Date()
    });
    
    // Auto-expire pending commands after 5 minutes
    setTimeout(() => {
      if (this.pendingCommands.has(commandId)) {
        this.pendingCommands.delete(commandId);
        console.log(`Command ${commandId} expired from pending commands`);
      }
    }, 5 * 60 * 1000);
  }

  /**
   * Send a command to an agent
   * @param {number} computerId - The target computer ID
   * @param {string} command - The command to execute
   * @param {string} commandId - The command ID
   * @returns {boolean} True if the command was sent
   */
  sendCommandToAgent(computerId, command, commandId) {
    try {
      if (!this.io) return false;
      
      const socketId = this.getAgentSocketId(computerId);
      
      if (!socketId) {
        console.log(`Cannot send command to computer ${computerId}: Agent not connected`);
        return false;
      }
      
      // Send the command to the agent
      this.io.to(socketId).emit('command:execute', {
        commandId,
        command
      });
      
      console.log(`Command ${commandId} sent to computer ${computerId}`);
      return true;
    } catch (error) {
      console.error(`Error sending command to computer ${computerId}:`, error);
      return false;
    }
  }

  /**
   * Notify command completion to the user
   * @param {string} commandId - The command ID
   * @param {Object} result - The command execution result
   */
  notifyCommandCompletion(commandId, result) {
    try {
      if (!this.io) return;
      
      const pendingCommand = this.pendingCommands.get(commandId);
      
      if (!pendingCommand) {
        console.log(`Command ${commandId} not found in pending commands`);
        return;
      }
      
      const { userId, computerId } = pendingCommand;
      
      // Remove from pending commands
      this.pendingCommands.delete(commandId);
      
      // Notify the user who initiated the command
      this.io.to(`user_${userId}`).emit('command:completed', {
        commandId,
        computerId,
        ...result
      });
      
      console.log(`Command ${commandId} completion notified to user ${userId}`);
    } catch (error) {
      console.error(`Error notifying command completion for ${commandId}:`, error);
    }
  }

  /**
   * Get the room ID for a computer
   * @param {number} computerId - The computer ID
   * @returns {Promise<number|null>} The room ID or null
   * @private
   */
  async getComputerRoomId(computerId) {
    try {
      // Import computerService only when needed to avoid circular dependencies
      const computerService = require('./computer.service');
      
      // Get computer info from database
      const computer = await computerService.getComputerById(computerId);
      return computer?.room_id || null;
    } catch (error) {
      console.error(`Error getting room ID for computer ${computerId}:`, error);
      return null;
    }
  }

  /**
   * Send a command to all computers in a room
   * @param {number} roomId - The room ID
   * @param {string} command - The command to execute
   * @param {number} userId - The user ID who initiated the command
   * @returns {Promise<Array<number>>} Array of computer IDs that received the command
   */
  async sendCommandToRoomComputers(roomId, command, userId) {
    try {
      // Import models only when needed to avoid circular dependencies
      const db = require('../database/models');
      const { v4: uuidv4 } = require('uuid');
      
      // Find all computers in the room
      const computers = await db.Computer.findAll({ 
        where: { room_id: roomId },
        attributes: ['id']
      });
      
      const sentComputerIds = [];
      
      // Loop through each computer and send command if online
      for (const computer of computers) {
        const computerId = computer.id;
        const isOnline = this.getAgentOnlineStatus(computerId);
        
        if (isOnline) {
          // Generate unique command ID for each computer
          const commandId = uuidv4();
          
          // Store the pending command
          this.storePendingCommand(commandId, userId, computerId);
          
          // Send the command to the agent
          const sent = this.sendCommandToAgent(computerId, command, commandId);
          
          if (sent) {
            sentComputerIds.push(computerId);
          }
        }
      }
      
      console.log(`Command sent to ${sentComputerIds.length} of ${computers.length} computers in room ${roomId}`);
      return sentComputerIds;
    } catch (error) {
      console.error(`Error sending command to room ${roomId}:`, error);
      throw new Error(`Failed to send command to room: ${error.message}`);
    }
  }
}

module.exports = new WebSocketService();