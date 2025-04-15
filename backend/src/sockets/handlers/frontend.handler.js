/**
 * WebSocket event handlers for frontend clients (admins/users).
 * Manages authentication, computer status subscriptions and command execution.
 */
const jwt = require('jsonwebtoken');
const { v4: uuidv4 } = require('uuid');
const websocketService = require('../../services/websocket.service');
const computerService = require('../../services/computer.service');
const config = require('../../config/auth.config');

/**
 * Sets up WebSocket event handlers for a connected frontend socket.
 * Initializes authentication, subscription, and command handling.
 * 
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client
 */
const setupFrontendHandlers = (socket) => {

  /**
   * Handles authentication requests from frontend clients using JWT.
   * Verifies token validity and assigns appropriate rooms based on user role.
   * 
   * @listens frontend:authenticate
   * @emits auth_response - Authentication result with status and user data
   */
  socket.on('frontend:authenticate', async (data) => {
    try {
      const token = data?.token;

      if (!token) {
        console.warn(`Frontend authentication attempt with no token from ${socket.id}`);
        socket.emit('auth_response', { status: 'error', message: 'Authentication token is required' });
        return;
      }

      jwt.verify(token, config.secret, (err, decoded) => {
        if (err) {
          console.warn(`Invalid token authentication attempt from ${socket.id}: ${err.message}`);
          socket.emit('auth_response', { status: 'error', message: 'Invalid or expired token' });
          return;
        }

        socket.data.userId = decoded.id;
        socket.data.role = decoded.role;
        console.log(decoded)

        socket.join(`user_${decoded.id}`);

        if (decoded.role === 'admin') {
          socket.join(websocketService.ROOM_PREFIXES.ADMIN);
        }

        socket.emit('auth_response', {
          status: 'success',
          message: 'Authentication successful',
          userId: decoded.id,
          role: decoded.role
        });

        console.info(`Frontend user authenticated: User ID ${decoded.id} (Role: ${decoded.role}), Socket ID ${socket.id}`);
      });
    } catch (error) {
      console.error(`Frontend authentication error for socket ${socket.id}: ${error.message}`, error.stack);
      socket.emit('auth_response', { status: 'error', message: 'Internal server error during authentication' });
    }
  });

  /**
   * Handles requests from frontend clients to subscribe to computer status updates.
   * Verifies user has access to the computer and sends current status.
   * 
   * @listens frontend:subscribe
   * @emits subscribe_response - Subscription result with status and computer ID
   * @emits computer:status_updated - Initial computer status data
   */
  socket.on('frontend:subscribe', async (payload) => {
    const computerId = payload?.computerId;
    const userId = socket.data.userId;

    if (!userId) {
      console.warn(`Unauthenticated subscription attempt from ${socket.id}`);
      socket.emit('subscribe_response', { status: 'error', message: 'Not authenticated' });
      return;
    }

    if (!computerId || typeof computerId !== 'number') {
      console.warn(`Invalid subscription attempt from ${socket.id}: Invalid or missing computerId`);
      socket.emit('subscribe_response', { status: 'error', message: 'Valid Computer ID is required' });
      return;
    }

    try {
      const isAdmin = socket.data.role === 'admin';
      let hasAccess = isAdmin;

      if (!hasAccess) {
        hasAccess = await computerService.checkUserComputerAccess(userId, computerId);
      }

      if (hasAccess) {
        const roomName = websocketService.ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId);
        socket.join(roomName);
        socket.emit('subscribe_response', { status: 'success', computerId });
        console.info(`User ${userId} (Socket ${socket.id}) subscribed to computer ${computerId} (Room: ${roomName})`);

        const currentStatus = websocketService.getAgentRealtimeStatus(computerId);
        if (currentStatus) {
             socket.emit(websocketService.EVENTS.COMPUTER_STATUS_UPDATED, {
                computerId,
                status: "online",
                ...currentStatus,
             });
        }

      } else {
        socket.emit('subscribe_response', { status: 'error', message: 'Access denied to this computer', computerId });
        console.warn(`User ${userId} (Socket ${socket.id}) denied subscription to computer ${computerId}`);
      }
    } catch (error) {
      console.error(`Subscription error for user ${userId}, computer ${computerId} (Socket ${socket.id}): ${error.message}`, error.stack);
      socket.emit('subscribe_response', { status: 'error', message: 'Subscription failed due to server error', computerId });
    }
  });

  /**
   * Handles requests from frontend clients to unsubscribe from computer status updates.
   * Removes client from the computer's subscriber room.
   * 
   * @listens frontend:unsubscribe
   * @emits unsubscribe_response - Unsubscription result
   */
  socket.on('frontend:unsubscribe', (payload) => {
    const computerId = payload?.computerId;

     if (!computerId || typeof computerId !== 'number') {
      console.warn(`Invalid unsubscribe attempt from ${socket.id}: Invalid or missing computerId`);
      socket.emit('unsubscribe_response', { status: 'error', message: 'Valid Computer ID is required' });
      return;
    }

    try {
      const roomName = websocketService.ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId);
      socket.leave(roomName);
      socket.emit('unsubscribe_response', { status: 'success', computerId });
      console.info(`Client ${socket.id} unsubscribed from computer ${computerId} (Room: ${roomName})`);
    } catch (error) {
      console.error(`Unsubscription error for computer ${computerId} (Socket ${socket.id}): ${error.message}`, error.stack);
      socket.emit('unsubscribe_response', { status: 'error', message: 'Unsubscription failed', computerId });
    }
  });

  /**
   * Handles requests from frontend clients to send a command to a specific agent.
   * Verifies user has access, forwards command, and uses acknowledgement for response.
   *
   * @listens frontend:send_command
   * @param {object} payload - The command payload { computerId, command }.
   * @param {function} ack - The Socket.IO acknowledgement callback.
   */
  socket.on('frontend:send_command', async (payload, ack) => { // Add ack parameter
    const { computerId, command, commandType = 'console' } = payload || {};
    const userId = socket.data.userId;

    // Ensure ack is a function before proceeding
    if (typeof ack !== 'function') {
        console.warn(`frontend:send_command received without acknowledgement callback from socket ${socket.id}`);
        return; // Cannot proceed without ack
    }

    // --- Input Validations ---
    if (!userId) {
      console.warn(`Unauthenticated command attempt from ${socket.id}`);
      ack({ status: 'error', message: 'Not authenticated' }); // Use ack for error
      return;
    }
    if (!computerId || typeof computerId !== 'number') {
      console.warn(`Invalid command attempt from user ${userId} (Socket ${socket.id}): Invalid or missing computerId`);
      ack({ status: 'error', message: 'Valid Computer ID is required' }); // Use ack for error
      return;
    }
    if (!command || typeof command !== 'string' || command.trim() === '') {
      console.warn(`Invalid command attempt from user ${userId} (Socket ${socket.id}): Missing or empty command`);
      ack({ status: 'error', message: 'Command content is required', computerId }); // Use ack for error
      return;
    }

    try {
      // --- Access Control ---
      const isAdmin = socket.data.role === 'admin';
      let hasAccess = isAdmin;
      if (!hasAccess) {
        hasAccess = await computerService.checkUserComputerAccess(userId, computerId);
      }
      if (!hasAccess) {
        console.warn(`Command access denied for user ${userId} to computer ${computerId} (Socket ${socket.id})`);
        ack({ status: 'error', message: 'Access denied to send commands to this computer', computerId }); // Use ack for error
        return;
      }

      // --- Command Processing ---
      const commandId = uuidv4(); // Generate command ID

      // Store pending command info (optional, good practice)
      websocketService.storePendingCommand(commandId, userId, computerId);

      // Attempt to send command to agent
      const sent = websocketService.sendCommandToAgent(computerId, command, commandId, commandType);

      // --- Respond via Acknowledgement ---
      if (sent) {
        console.info(`Command ${commandId} (type: ${commandType}) initiated by user ${userId} sent towards computer ${computerId} (Socket ${socket.id})`);
        // Acknowledge success with commandId
        ack({ status: 'success', computerId, commandId, commandType });
        // NOTE: No separate 'command_sent' emit needed here for success
      } else {
        console.warn(`Failed to send command ${commandId} (type: ${commandType}) from user ${userId} to computer ${computerId}: Agent not connected (Socket ${socket.id})`);
        // Clean up pending command if send failed immediately
        websocketService.pendingCommands.delete(commandId); // Assuming delete method exists
        // Acknowledge failure
        ack({ status: 'error', message: 'Agent is not connected', computerId, commandId, commandType }); // Include commandId even on error if generated
      }

    } catch (error) {
      console.error(`Send command error for user ${userId}, computer ${computerId} (Socket ${socket.id}): ${error.message}`, error.stack);
      // Acknowledge server error
      ack({ status: 'error', message: 'Failed to send command due to server error', computerId });
    }
  });
};

/**
 * Handles disconnection logic for frontend clients.
 * Logs disconnection events for audit and troubleshooting.
 * 
 * @param {import("socket.io").Socket} socket - The disconnected socket instance
 */
const handleFrontendDisconnect = (socket) => {
  console.info(`Frontend client disconnected: User ID ${socket.data.userId || 'N/A'}, Socket ID ${socket.id}`);
};

module.exports = {
  setupFrontendHandlers,
  handleFrontendDisconnect
};
