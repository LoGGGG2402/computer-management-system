/**
 * WebSocket event handlers for agent clients.
 */
const computerService = require('../../services/computer.service');
const websocketService = require('../../services/websocket.service');
const logger = require('../../utils/logger');

// --- Handler Functions ---

/**
 * Handles agent authentication requests. Verifies agent ID and token.
 * @param {import("socket.io").Socket} socket - The socket instance for the agent client.
 * @param {object} data - Authentication data containing { agentId, token }.
 */
async function handleAgentAuthentication(socket, data) {
  try {
    const { agentId, token } = data || {};

    if (!agentId || !token) {
      logger.warn(`Authentication attempt with missing credentials from ${socket.id}`);
      socket.emit('agent:ws_auth_failed', { status: 'error', message: 'Missing agent ID or token' });
      return;
    }

    const computerId = await computerService.verifyAgentToken(agentId, token);

    if (!computerId) {
      logger.warn(`Failed authentication attempt for agent ${agentId} from ${socket.id}`);
      socket.emit('agent:ws_auth_failed', { status: 'error', message: 'Authentication failed (Invalid ID or token)' });
      return;
    }

    socket.data.computerId = computerId;
    socket.data.agentId = agentId;

    websocketService.joinAgentRoom(socket, computerId);

    socket.emit('agent:ws_auth_success', {
      status: 'success',
      message: 'Authentication successful'
    });
  } catch (error) {
    logger.error(`Agent authentication error for agent ${data?.agentId}, socket ${socket.id}:`, { 
      error: error.message, 
      stack: error.stack 
    });
    socket.emit('agent:ws_auth_failed', { status: 'error', message: 'Internal server error during authentication' });
  }
}

/**
 * Handles status updates (CPU, RAM, Disk usage) received from an authenticated agent.
 * @param {import("socket.io").Socket} socket - The socket instance for the agent client.
 * @param {object} data - Status update data containing { cpuUsage, ramUsage, diskUsage }.
 */
async function handleAgentStatusUpdate(socket, data) {
  const computerId = socket.data.computerId;

  if (!computerId) {
    logger.warn(`Unauthenticated status update attempt from ${socket.id}. Ignoring.`);
    return;
  }

  if (!data) {
      logger.warn(`Received empty status update from computer ${computerId} (Socket ${socket.id})`);
      return;
  }

  try {
    websocketService.updateRealtimeCache(computerId, {
      cpuUsage: data.cpuUsage,
      ramUsage: data.ramUsage,
      diskUsage: data.diskUsage,
    });

    await websocketService.broadcastStatusUpdate(computerId);
  } catch (error) {
    logger.error(`Status update processing error for computer ${computerId} (Socket ${socket.id}):`, {
      error: error.message,
      stack: error.stack
    });
  }
}

/**
 * Handles command execution results received from an authenticated agent.
 * @param {import("socket.io").Socket} socket - The socket instance for the agent client.
 * @param {object} data - Command result data containing:
 *  - commandId: string - unique ID of the executed command
 *  - type: string - command type (e.g., 'console')
 *  - success: boolean - whether the command was successful
 *  - result: object - detailed result object with stdout, stderr, exitCode
 */
function handleAgentCommandResult(socket, data) {
  const computerId = socket.data.computerId;

  if (!computerId) {
    logger.warn(`Unauthenticated command result received from ${socket.id}. Ignoring.`);
    return;
  }

  const { commandId, type, success, result } = data || {};

  if (!commandId) {
    logger.warn(`Command result missing commandId from computer ${computerId} (Socket ${socket.id}). Ignoring.`);
    return;
  }

  logger.debug(`Command result for ID ${commandId} received from computer ${computerId} (Socket ${socket.id}) - Type: ${type || 'unknown'}, Success: ${success}`);

  try {
    // Standardize the result format for the frontend
    const standardizedResult = {
      commandId,
      type: type || 'console',
      success: !!success,
      result
    };

    websocketService.notifyCommandCompletion(commandId, standardizedResult);

  } catch (error) {
    logger.error(`Command result handling error for command ${commandId}, computer ${computerId} (Socket ${socket.id}):`, {
      error: error.message,
      stack: error.stack
    });
  }
}

// --- Setup and Disconnect ---

/**
 * Sets up WebSocket event handlers for a connected agent socket.
 * @param {import("socket.io").Server} io - The Socket.IO server instance (passed but not used directly here).
 * @param {import("socket.io").Socket} socket - The socket instance for the agent client.
 */
const setupAgentHandlers = (io, socket) => {
  const agentIdFromData = socket.data.agentId;
  const tokenFromData = socket.data.authToken;

  if (agentIdFromData && tokenFromData) {
    logger.info(`Attempting auto-authentication for agent via headers: Agent ID ${agentIdFromData}, Socket ID ${socket.id}`);
    handleAgentAuthentication(socket, {
      agentId: agentIdFromData,
      token: tokenFromData
    });
  } else {
    logger.info(`Agent ${socket.id} connected without pre-authentication headers. Waiting for 'agent:authenticate' event.`);
  }

  socket.on('agent:authenticate', (data) => {
    if (socket.data.computerId) {
        logger.warn(`Agent ${socket.data.agentId} (Socket ${socket.id}) sent 'agent:authenticate' but is already authenticated. Ignoring.`);
        return;
    }
    handleAgentAuthentication(socket, data);
  });

  socket.on('agent:status_update', (data) => {
    handleAgentStatusUpdate(socket, data);
  });

  socket.on('agent:command_result', (data) => {
    handleAgentCommandResult(socket, data);
  });

};

/**
 * Handles disconnection logic for agent clients. Delegates to the WebSocket service.
 * @param {import("socket.io").Socket} socket - The disconnected socket instance.
 */
const handleAgentDisconnect = (socket) => {
  websocketService.handleAgentDisconnect(socket);
};

module.exports = {
  setupAgentHandlers,
  handleAgentDisconnect
};
