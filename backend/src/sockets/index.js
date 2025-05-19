/**
 * WebSocket Initialization and Connection Routing Logic.
 */
const {
  setupFrontendHandlers,
  handleFrontendDisconnect,
} = require("./handlers/frontend.handler");
const {
  setupAgentHandlers,
  handleAgentDisconnect,
} = require("./handlers/agent.handler");
const websocketService = require("../services/websocket.service");
const logger = require("../utils/logger");

/**
 * Initializes WebSocket event handlers and middleware.
 * @param {import("socket.io").Server} io - The Socket.IO server instance.
 */
const initializeWebSocket = (io) => {
  websocketService.setIo(io);

  io.on("connection", (socket) => {
    const clientId = socket.id;
    const clientIp = socket.handshake.address;

    logger.info(
      `New client connected: ${clientId} (IP: ${clientIp}, Type: ${socket.data.type})`
    );

    if (socket.data.type === "agent") {
      logger.debug(`Setting up agent handlers for socket ${clientId}`);
      setupAgentHandlers(socket);
    } else if (socket.data.type === "frontend") {
      logger.debug(`Setting up frontend handlers for socket ${clientId}`);
      setupFrontendHandlers(socket);
    } else {
      logger.warn(
        `Unknown client type '${socket.data.type}' for socket ${clientId}. Disconnecting.`
      );
      socket.disconnect(true);
      return;
    }

    socket.on("disconnect", (reason) => {
      logger.info(
        `Client disconnected: ${clientId} (Type: ${socket.data.type}), Reason: ${reason}`
      );
      handleDisconnect(socket, reason);
    });

    socket.on("error", (error) => {
      logger.error(`Socket ${clientId} error:`, {
        error: error.message,
        stack: error.stack,
      });
    });
  });

  io.use((socket, next) => {
    const clientType = socket.handshake.headers["x-client-type"]?.toLowerCase();
    const authHeader = socket.handshake.headers.authorization;

    if (!clientType) {
      logger.warn(
        `Connection attempt without X-Client-Type header, ID: ${socket.id}`
      );
      return next(
        new Error("Authentication failed: Missing X-Client-Type header")
      );
    }

    socket.data.type = clientType;

    if (clientType === "agent") {
      const agentId = socket.handshake.headers["x-agent-id"];

      if (!agentId) {
        logger.warn(
          `Agent connection attempt without X-Agent-ID header, ID: ${socket.id}`
        );
        return next(
          new Error("Authentication failed: Missing required headers")
        );
      }
      socket.data.agentId = agentId;

      if (!authHeader?.startsWith("Bearer ")) {
        logger.warn(
          `Agent connection attempt without Bearer token, Agent ID: ${agentId}, Socket ID: ${socket.id}`
        );
        return next(
          new Error("Authentication failed: Missing required headers")
        );
      }
    }

    if (authHeader?.startsWith("Bearer ")) {
      socket.data.authToken = authHeader.substring(7);
    } else if (clientType !== "agent") {
      logger.debug(
        `Client connection without Authorization header, Type: ${clientType}, ID: ${socket.id}`
      );
    }
    next();
  });

  logger.info("WebSocket server initialized successfully");
};

/**
 * Handles client disconnection routing based on the client type stored in socket data.
 * @param {import("socket.io").Socket} socket - The disconnected socket instance.
 * @param {string} reason - The reason for disconnection.
 */
const handleDisconnect = (socket, reason) => {
  const clientType = socket.data.type;

  if (clientType === "agent") {
    handleAgentDisconnect(socket);
  } else if (clientType === "frontend") {
    handleFrontendDisconnect(socket);
  } else {
    logger.info(
      `Unknown client type disconnected: ${socket.id}, Reason: ${reason}`
    );
  }
};

module.exports = { initializeWebSocket };
