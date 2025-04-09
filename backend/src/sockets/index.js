const { setupFrontendHandlers, handleFrontendDisconnect } = require('./handlers/frontend.handler');
const { setupAgentHandlers, handleAgentDisconnect } = require('./handlers/agent.handler');
const websocketService = require('../services/websocket.service');

/**
 * Initialize WebSocket handlers
 * @param {Object} io - Socket.IO server instance
 */
const initializeWebSocket = (io) => {
  // Set the io instance in the websocket service
  websocketService.setIo(io);
  
  // Connection event
  io.on('connection', (socket) => {
    console.log('New client connected:', socket.id);
    
    // Try to determine the client type from the handshake query or headers
    const queryType = socket.handshake.query?.clientType;
    const headerType = socket.handshake.headers['client-type'];
    const clientType = queryType || headerType || 'unknown';
    
    // Log connection information
    console.log(`Client connected with type: ${clientType}`);
    
    // Set up event handlers based on client type
    // Frontend handlers - will check authentication inside
    setupFrontendHandlers(io, socket);
    
    // Agent handlers - will check authentication inside
    setupAgentHandlers(io, socket);
    
    // Disconnection event
    socket.on('disconnect', () => {
      handleDisconnect(socket);
    });
  });
};

/**
 * Handle client disconnection
 * @param {Object} socket - Socket instance
 */
const handleDisconnect = (socket) => {
  console.log('Client disconnected:', socket.id);
  
  // Check the socket type and handle accordingly
  if (socket.data.type === 'frontend') {
    handleFrontendDisconnect(socket);
  } else if (socket.data.type === 'agent') {
    handleAgentDisconnect(socket);
  }
};

module.exports = { initializeWebSocket };