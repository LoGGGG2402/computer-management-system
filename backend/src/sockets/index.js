/**
 * WebSocket Initialization and Connection Routing Logic.
 */
const { setupFrontendHandlers, handleFrontendDisconnect } = require('./handlers/frontend.handler');
const { setupAgentHandlers, handleAgentDisconnect } = require('./handlers/agent.handler'); // Assuming this path is correct
const websocketService = require('../services/websocket.service');
const logger = require('../utils/logger');

/**
 * Initializes WebSocket event handlers and middleware.
 * @param {import("socket.io").Server} io - The Socket.IO server instance.
 */
const initializeWebSocket = (io) => {
  websocketService.setIo(io);

  io.on('connection', (socket) => {
    const clientId = socket.id;
    const clientIp = socket.handshake.address;
    const headerType = socket.handshake.headers['x-client-type'];
    const clientType = (headerType || 'unknown').toLowerCase();
    logger.info(`New client connected: ${clientId} (IP: ${clientIp}, Type: ${clientType})`);
    socket.data.type = clientType;

    if (clientType === 'agent') {
      logger.debug(`Setting up agent handlers for socket ${clientId}`);
      setupAgentHandlers(io, socket);
    } else if (clientType === 'frontend') {
      logger.debug(`Setting up frontend handlers for socket ${clientId}`);
      setupFrontendHandlers(socket);
    } else {
      logger.warn(`Unknown client type '${clientType}' for socket ${clientId}. Disconnecting.`);
      socket.emit('error', { message: 'Unknown client type' });
      socket.disconnect(true);
      return;
    }

    socket.on('disconnect', (reason) => {
      logger.info(`Client disconnected: ${clientId} (Type: ${clientType}), Reason: ${reason}`);
      handleDisconnect(socket, reason);
    });

    socket.on('error', (error) => {
      logger.error(`Socket ${clientId} error:`, {
        error: error.message,
        stack: error.stack 
      });
    });
  });

  io.use((socket, next) => {
    const authHeader = socket.handshake.headers.authorization;
    const agentIdHeader = socket.handshake.headers['x-agent-id'];

    if (authHeader?.startsWith('Bearer ')) {
      socket.data.authToken = authHeader.substring(7);
      logger.debug(`Auth token found for socket ${socket.id}`);
    }

    if (agentIdHeader) {
      socket.data.agentId = agentIdHeader;
      logger.debug(`Agent ID header found for socket ${socket.id}: ${agentIdHeader}`);
    }

    next();
  });

  logger.info('WebSocket server initialized successfully');
};

/**
 * Handles client disconnection routing based on the client type stored in socket data.
 * @param {import("socket.io").Socket} socket - The disconnected socket instance.
 * @param {string} reason - The reason for disconnection.
 */
const handleDisconnect = (socket, reason) => {
  const clientType = socket.data.type;

  if (clientType === 'agent') {
    handleAgentDisconnect(socket);
  } else if (clientType === 'frontend') {
    handleFrontendDisconnect(socket);
  } else {
    logger.info(`Unknown client type disconnected: ${socket.id}, Reason: ${reason}`);
  }
};

module.exports = { initializeWebSocket };
