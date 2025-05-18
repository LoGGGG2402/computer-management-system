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
    const { agentId, authToken } = data || {};

    if (!agentId || !authToken) {
      logger.warn(`Authentication attempt with missing credentials from ${socket.id}`);
      socket.emit('connect_error', { 
        message: 'Authentication failed: Missing required headers' 
      });
      return;
    }

    const computerId = await computerService.verifyAgentToken(agentId, authToken);

    if (!computerId) {
      logger.warn(`Failed authentication attempt for agent ${agentId} from ${socket.id}`);
      socket.emit('connect_error', {
        message: 'Authentication failed: Invalid agent credentials' 
      });
      return;
    }

    socket.data.computerId = computerId;
    socket.data.agentId = agentId;

    websocketService.joinAgentRoom(socket, computerId);
  } catch (error) {
    logger.error(`Agent authentication error for agent ${data?.agentId}, socket ${socket.id}:`, {
      error: error.message,
      stack: error.stack
    });
    socket.emit('connect_error', {
      message: 'Internal error: Unable to establish WebSocket connection' 
    });
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

  // Validate data according to API constraints
  const { cpuUsage, ramUsage, diskUsage } = data;

  // Check that each value is a number between 0.0 and 100.0
  const isValidPercentage = (value) => typeof value === 'number' && value >= 0.0 && value <= 100.0;
  
  if (!isValidPercentage(cpuUsage) || !isValidPercentage(ramUsage) || !isValidPercentage(diskUsage)) {
    logger.warn(`Invalid status data received from computer ${computerId} (Socket ${socket.id}): Values must be numbers between 0.0 and 100.0`);
    return;
  }

  try {
    websocketService.updateRealtimeCache(computerId, {
      cpuUsage,
      ramUsage,
      diskUsage
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

  const { commandId, commandType, success, result } = data || {};

  // Validate according to API constraints
  if (!commandId || typeof commandId !== 'string') {
    logger.warn(`Command result missing or invalid commandId from computer ${computerId} (Socket ${socket.id}). Ignoring.`);
    return;
  }

  // Validate result object
  if (!result || typeof result !== 'object') {
    logger.warn(`Command result missing result object from computer ${computerId} (Socket ${socket.id}).`);
    return;
  }


  logger.debug(`Command result for ID ${commandId} received from computer ${computerId} (Socket ${socket.id}) - Type: ${commandType}, Success: ${success}`);

  try {
    // Standardize the result format for the frontend
    const standardizedResult = {
      commandId,
      commandType,
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
 * @param {import("socket.io").Socket} socket - The socket instance for the agent client.
 */
const setupAgentHandlers = (socket) => {
  const agentId = socket.data.agentId;
  const authToken = socket.data.authToken;

  // According to documentation, authentication is handled entirely through WebSocket connection headers
  if (agentId && authToken) {
    logger.info(`Processing agent authentication via headers: Agent ID ${agentId}, Socket ID ${socket.id}`);
    handleAgentAuthentication(socket, {
      agentId,
      authToken
    });
  } else {
    logger.warn(`No agent ID or token provided for socket ${socket.id}. Disconnecting.`);
    socket.disconnect();
    return;
  }

  socket.on(websocketService.EVENTS.AGENT_STATUS_UPDATE, (data) => {
    handleAgentStatusUpdate(socket, data);
  });

  socket.on(websocketService.EVENTS.AGENT_COMMAND_RESULT, (data) => {
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
