/**
 * Handler for agent WebSocket connections
 */
const websocketService = require('../../services/websocket.service');
const computerService = require('../../services/computer.service');

/**
 * Set up agent WebSocket event handlers
 * @param {Object} io - Socket.IO server instance
 * @param {Object} socket - Socket instance
 */
const setupAgentHandlers = (io, socket) => {
  // Agent WebSocket authentication
  socket.on('agent:authenticate_ws', async (payload) => {
    try {
      await handleAgentAuthenticate(socket, payload);
    } catch (error) {
      console.error('Agent authentication error:', error);
      socket.emit('agent:ws_auth_failed', { 
        message: 'Authentication failed due to server error' 
      });
      socket.disconnect(true);
    }
  });

  // Agent command result
  socket.on('agent:command_result', (payload) => {
    try {
      const { commandId, stdout, stderr, exitCode } = payload;
      
      // Notify command completion
      websocketService.notifyCommandCompletion(commandId, {
        stdout,
        stderr,
        exitCode
      });
    } catch (error) {
      console.error('Command result error:', error);
    }
  });
  
  // Agent status update - now fully handled via WebSocket
  socket.on('agent:status_update', async (payload) => {
    try {
      const computerId = socket.data.computerId;
      
      if (!computerId) {
        return;
      }
      
      const { cpuUsage, ramUsage, diskUsage } = payload;
      
      // Update the last seen timestamp in database
      await computerService.updateLastSeen(computerId);
      
      // Update realtime cache
      websocketService.updateRealtimeCache(computerId, {
        cpuUsage,
        ramUsage,
        diskUsage,
        lastSeen: new Date()
      });
      
      // Broadcast status update
      await websocketService.broadcastStatusUpdate(computerId);
      
      console.log(`Received status update from computer ${computerId}: CPU: ${cpuUsage}%, RAM: ${ramUsage}%`);
    } catch (error) {
      console.error('Status update error:', error);
    }
  });
};

/**
 * Handle agent authentication
 * @param {Object} socket - Socket instance
 * @param {Object} payload - Authentication payload
 */
const handleAgentAuthenticate = async (socket, payload) => {
  const { agentId, token } = payload;
  
  if (!agentId || !token) {
    socket.emit('agent:ws_auth_failed', { 
      message: 'Agent ID and token are required' 
    });
    socket.disconnect(true);
    return;
  }
  
  // Verify the agent token
  const computerId = await computerService.verifyAgentToken(agentId, token);
  
  if (computerId) {
    // Store computer ID in socket data
    socket.data.computerId = computerId;
    socket.data.agentId = agentId;
    socket.data.type = 'agent';
    
    // Register the agent socket
    websocketService.registerAgentSocket(computerId, socket.id);
    
    // Join computer-specific room
    socket.join(`computer_${computerId}`);
    
    // Send success response
    socket.emit('agent:ws_auth_success', { 
      computerId 
    });
    
    // Update and broadcast online status
    await websocketService.updateAndBroadcastOnlineStatus(computerId);
    
    console.log(`Agent authenticated: ${agentId} (Computer ID: ${computerId})`);
  } else {
    socket.emit('agent:ws_auth_failed', { 
      message: 'Invalid agent token' 
    });
    socket.disconnect(true);
  }
};

/**
 * Handle agent disconnection
 * @param {Object} socket - Socket instance
 */
const handleAgentDisconnect = (socket) => {
  console.log('Agent client disconnected:', socket.id);
  
  // Update agent status to offline and notify
  websocketService.handleAgentDisconnect(socket.id);
};

module.exports = {
  setupAgentHandlers,
  handleAgentDisconnect
};