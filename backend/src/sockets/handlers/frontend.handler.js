/**
 * WebSocket event handlers for frontend clients (admins/users).
 * Manages authentication, computer status subscriptions and command execution.
 */
const jwt = require('jsonwebtoken');
const { v4: uuidv4 } = require('uuid');
const websocketService = require('../../services/websocket.service');
const computerService = require('../../services/computer.service');
const config = require('../../config/auth.config');
const logger = require('../../utils/logger');
const validationUtils = require('../../utils/validation.utils');

// --- Handler Functions ---

/**
 * Handles frontend authentication using JWT from the socket authorization header.
 * Verifies token validity and assigns appropriate rooms based on user role.
 *
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client.
 */
async function handleFrontendAuthentication(socket, data) {
  try {
    const {authToken} = data || {};

    if (!authToken) {
      logger.warn(`Frontend authentication attempt with no token from ${socket.id}`);
      socket.emit('connect_error', { message: 'Authentication failed: Missing Authorization header' });
      socket.disconnect();
      return;
    }

    jwt.verify(authToken, config.secret, (err, decoded) => {
      if (err) {
        // Differentiate between expired token and invalid token
        const errorMessage = err.name === 'TokenExpiredError' 
          ? 'Authentication failed: Token expired'
          : 'Authentication failed: Invalid token';
          
        logger.warn(`Token authentication attempt failed from ${socket.id}: ${err.message}`);
        socket.emit('connect_error', { message: errorMessage });
        socket.disconnect();
        return;
      }

      socket.data.userId = decoded.id;
      socket.data.role = decoded.role;

      websocketService.joinUserRoom(socket, decoded.id);

      if (decoded.role === 'admin') {
        websocketService.joinAdminRoom(socket);
      }
      logger.info(`Frontend user authenticated: User ID ${decoded.id} (Role: ${decoded.role}), Socket ID ${socket.id}`);
    });
  } catch (error) {
    logger.error(`Frontend authentication error for socket ${socket.id}:`, {
      error: error.message,
      stack: error.stack
    });
    socket.emit('connect_error', { message: 'Internal error: Unable to establish WebSocket connection' });
    socket.disconnect();
  }
}

/**
 * Handles requests from frontend clients to subscribe to computer status updates.
 * Verifies user has access to the computer and sends current status.
 * 
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client
 * @param {object} data - The subscription data { computerId }
 */
async function handleComputerSubscription(socket, data) {
  const computerId = data?.computerId;
  const userId = socket.data.userId;

  if (!userId) {
    logger.warn(`Unauthenticated subscription attempt from ${socket.id}`);
    socket.emit('subscribe_response', { status: 'error', message: 'Not authenticated' });
    return;
  }

  const computerIdError = validationUtils.validatePositiveIntegerId(computerId, 'Computer ID');
  if (computerIdError) {
    logger.warn(`Invalid subscription attempt from ${socket.id}: ${computerIdError}`);
    socket.emit('subscribe_response', { status: 'error', message: computerIdError });
    return;
  }

  try {
    const isAdmin = socket.data.role === 'admin';
    let hasAccess = isAdmin;

    if (!hasAccess) {
      hasAccess = await computerService.checkUserComputerAccess(userId, computerId);
    }

    if (hasAccess) {
      websocketService.joinComputerRoom(socket, computerId);
      socket.emit('subscribe_response', { status: 'success', computerId });

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
      logger.warn(`User ${userId} (Socket ${socket.id}) denied subscription to computer ${computerId}`);
    }
  } catch (error) {
    logger.error(`Subscription error for user ${userId}, computer ${computerId} (Socket ${socket.id}):`, { 
      error: error.message, 
      stack: error.stack 
    });
    socket.emit('subscribe_response', { status: 'error', message: 'Subscription failed due to server error', computerId });
  }
}

/**
 * Handles requests from frontend clients to unsubscribe from computer status updates.
 * Removes client from the computer's subscriber room.
 * 
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client
 * @param {object} data - The unsubscription data { computerId }
 */
function handleComputerUnsubscription(socket, data) {
  const computerId = data?.computerId;

  const computerIdError = validationUtils.validatePositiveIntegerId(computerId, 'Computer ID');
  if (computerIdError) {
    logger.warn(`Invalid unsubscribe attempt from ${socket.id}: ${computerIdError}`);
    socket.emit('unsubscribe_response', { status: 'error', message: computerIdError });
    return;
  }

  try {
    const roomName = websocketService.ROOM_PREFIXES.COMPUTER_SUBSCRIBERS(computerId);
    socket.leave(roomName);
    socket.emit('unsubscribe_response', { status: 'success', computerId });
    logger.info(`Client ${socket.id} unsubscribed from computer ${computerId} (Room: ${roomName})`);
  } catch (error) {
    logger.error(`Unsubscription error for computer ${computerId} (Socket ${socket.id}):`, { 
      error: error.message, 
      stack: error.stack 
    });
    socket.emit('unsubscribe_response', { status: 'error', message: 'Unsubscription failed', computerId });
  }
}

/**
 * Handles requests from frontend clients to send a command to a specific agent.
 * Verifies user has access, forwards command, and uses acknowledgement for response.
 *
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client
 * @param {object} data - The command data { computerId, command, commandType }
 * @param {function} ack - The Socket.IO acknowledgement callback
 */
async function handleCommandSend(socket, data, ack) {
  const { computerId, command, commandType = 'console' } = data || {};
  const userId = socket.data.userId;

  // Ensure ack is a function before proceeding
  if (typeof ack !== 'function') {
      logger.warn(`frontend:send_command received without acknowledgement callback from socket ${socket.id}`);
      return; // Cannot proceed without ack
  }

  // --- Input Validations ---
  if (!userId) {
    logger.warn(`Unauthenticated command attempt from ${socket.id}`);
    ack({ status: 'error', message: 'Not authenticated' }); // Use ack for error
    return;
  }

  const computerIdError = validationUtils.validatePositiveIntegerId(computerId, 'Computer ID');
  if (computerIdError) {
    logger.warn(`Invalid command attempt from user ${userId} (Socket ${socket.id}): ${computerIdError}`);
    ack({ status: 'error', message: computerIdError }); // Use ack for error
    return;
  }
  
  // Command validation based on API constraints
  if (!command || typeof command !== 'string') {
    logger.warn(`Invalid command attempt from user ${userId} (Socket ${socket.id}): Missing or invalid command type`);
    ack({ status: 'error', message: 'Command must be a string', computerId }); // Use ack for error
    return;
  }
  
  if (command.trim() === '' || command.length > 2000) {
    logger.warn(`Invalid command attempt from user ${userId} (Socket ${socket.id}): Empty or too long command (max 2000 chars)`);
    ack({ status: 'error', message: 'Command must be non-empty and maximum 2000 characters', computerId });
    return;
  }
  
  // CommandType validation
  const validCommandTypes = ['console', 'powershell', 'cmd', 'bash', 'system', 'service'];
  if (!validCommandTypes.includes(commandType)) {
    logger.warn(`Invalid command type "${commandType}" from user ${userId} (Socket ${socket.id})`);
    ack({ status: 'error', message: `Invalid command type. Must be one of: ${validCommandTypes.join(', ')}`, computerId });
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
      logger.warn(`Command access denied for user ${userId} to computer ${computerId} (Socket ${socket.id})`);
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
      logger.info(`Command ${commandId} (type: ${commandType}) initiated by user ${userId} sent towards computer ${computerId} (Socket ${socket.id})`);
      // Acknowledge success with commandId
      ack({ status: 'success', computerId, commandId, commandType });
      // NOTE: No separate 'command_sent' emit needed here for success
    } else {
      logger.warn(`Failed to send command ${commandId} (type: ${commandType}) from user ${userId} to computer ${computerId}: Agent not connected (Socket ${socket.id})`);
      // Clean up pending command if send failed immediately
      websocketService.pendingCommands.delete(commandId); // Assuming delete method exists
      // Acknowledge failure
      ack({ status: 'error', message: 'Agent is not connected', computerId, commandId, commandType }); // Include commandId even on error if generated
    }

  } catch (error) {
    logger.error(`Send command error for user ${userId}, computer ${computerId} (Socket ${socket.id}):`, { 
      error: error.message, 
      stack: error.stack 
    });
    // Acknowledge server error
    ack({ status: 'error', message: 'Failed to send command due to server error', computerId });
  }
}

// --- Setup and Disconnect ---

/**
 * Sets up WebSocket event handlers for a connected frontend socket.
 * Initializes authentication, subscription, and command handling.
 * 
 * @param {import("socket.io").Socket} socket - The socket instance for the frontend client
 */
const setupFrontendHandlers = (socket) => {
  const authToken = socket.data.authToken;
  // Get client type header to validate client type
  const clientType = socket.handshake.headers['x-client-type'];
  
  // Check client type first
  if (!clientType) {
    logger.warn(`Missing client type for socket ${socket.id}. Disconnecting.`);
    socket.emit('connect_error', { message: 'Authentication failed: Missing X-Client-Type header' });
    socket.disconnect();
    return;
  }
  
  // Validate client type
  if (clientType !== 'frontend') {
    logger.warn(`Invalid client type ${clientType} for socket ${socket.id}. Disconnecting.`);
    socket.emit('connect_error', { message: 'Authentication failed: Invalid X-Client-Type header' });
    socket.disconnect();
    return;
  }
  
  // Automatic authentication from headers
  if (authToken) {
    logger.info(`Attempting auto-authentication for frontend client via headers: Socket ID ${socket.id}`);
    handleFrontendAuthentication(socket, { authToken });
  } else {
    logger.warn(`No auth token provided for socket ${socket.id}. Disconnecting.`);
    socket.emit('connect_error', { message: 'Authentication failed: Missing Authorization header' });
    socket.disconnect();
    return;
  }

  // Register event handlers
  socket.on('frontend:subscribe', (data) => {
    handleComputerSubscription(socket, data);
  });

  socket.on('frontend:unsubscribe', (data) => {
    handleComputerUnsubscription(socket, data);
  });

  socket.on('frontend:send_command', (data, ack) => {
    handleCommandSend(socket, data, ack);
  });
};

/**
 * Handles disconnection logic for frontend clients.
 * Logs disconnection events for audit and troubleshooting.
 * 
 * @param {import("socket.io").Socket} socket - The disconnected socket instance
 */
const handleFrontendDisconnect = (socket) => {
  logger.info(`Frontend client disconnected: User ID ${socket.data.userId || 'N/A'}, Socket ID ${socket.id}`);
};

module.exports = {
  setupFrontendHandlers,
  handleFrontendDisconnect
};
