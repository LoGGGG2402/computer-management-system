/**
 * Handler for frontend WebSocket connections
 */
const jwt = require('jsonwebtoken');
const { v4: uuidv4 } = require('uuid');
const websocketService = require('../../services/websocket.service');
const roomService = require('../../services/room.service');
const config = require('../../config/auth.config');

/**
 * Set up frontend WebSocket event handlers
 * @param {Object} io - Socket.IO server instance
 * @param {Object} socket - Socket instance
 */
const setupFrontendHandlers = (io, socket) => {
  // Authentication for frontend users (admin/user)
  socket.on('frontend:authenticate', async (data) => {
    try {
      const { token } = data;
      
      if (!token) {
        socket.emit('auth_response', { 
          status: 'error', 
          message: 'No token provided' 
        });
        return;
      }
      
      // Verify the JWT token
      jwt.verify(token, config.secret, (err, decoded) => {
        if (err) {
          socket.emit('auth_response', { 
            status: 'error', 
            message: 'Invalid token' 
          });
          return;
        }
        
        // Store user info in socket data
        socket.data.userId = decoded.id;
        socket.data.role = decoded.role;
        socket.data.type = 'frontend';
        
        // If admin, register the admin socket
        if (decoded.role === 'admin') {
          websocketService.registerAdminSocket(socket.id);
          socket.join('admin');
        }
        
        socket.join(`user_${decoded.id}`);
        
        socket.emit('auth_response', { 
          status: 'success', 
          message: 'Authentication successful',
          userId: decoded.id,
          role: decoded.role
        });
        
        console.log(`Frontend user authenticated: ${decoded.id} (${decoded.role})`);
      });
    } catch (error) {
      console.error('Frontend authentication error:', error);
      socket.emit('auth_response', { 
        status: 'error', 
        message: 'Authentication failed' 
      });
    }
  });
  
  // Frontend subscription to rooms
  socket.on('frontend:subscribe', async (payload) => {
    try {
      await handleFrontendSubscribe(socket, payload);
    } catch (error) {
      console.error('Subscription error:', error);
      socket.emit('subscribe_response', { 
        status: 'error', 
        message: 'Subscription failed' 
      });
    }
  });
  
  // Frontend unsubscription from rooms
  socket.on('frontend:unsubscribe', (payload) => {
    try {
      const { roomIds } = payload;
      
      if (Array.isArray(roomIds)) {
        roomIds.forEach(roomId => {
          socket.leave(`room_${roomId}`);
          console.log(`Client ${socket.id} unsubscribed from room ${roomId}`);
        });
      }
      
      socket.emit('unsubscribe_response', { 
        status: 'success' 
      });
    } catch (error) {
      console.error('Unsubscription error:', error);
    }
  });
  
  // Frontend send command to agent
  socket.on('frontend:send_command', async (payload) => {
    try {
      const { computerId, command } = payload;
      const userId = socket.data.userId;
      
      if (!userId) {
        socket.emit('command_sent', { 
          status: 'error', 
          message: 'Not authenticated' 
        });
        return;
      }
      
      // Generate command ID
      const commandId = uuidv4();
      
      // Store the pending command
      websocketService.storePendingCommand(commandId, userId, computerId);
      
      // Send command to agent
      const sent = websocketService.sendCommandToAgent(computerId, command, commandId);
      
      if (sent) {
        socket.emit('command_sent', { 
          status: 'success', 
          computerId, 
          commandId 
        });
      } else {
        socket.emit('command_sent', { 
          status: 'error', 
          message: 'Agent not connected', 
          computerId 
        });
      }
    } catch (error) {
      console.error('Send command error:', error);
      socket.emit('command_sent', { 
        status: 'error', 
        message: 'Failed to send command' 
      });
    }
  });
};

/**
 * Handle frontend subscription to rooms
 * @param {Object} socket - Socket instance
 * @param {Object} payload - Subscription payload
 */
const handleFrontendSubscribe = async (socket, payload) => {
  const { roomIds } = payload;
  const userId = socket.data.userId;
  const isAdmin = socket.data.role === 'admin';
  
  if (!userId) {
    socket.emit('subscribe_response', { 
      status: 'error', 
      message: 'Not authenticated' 
    });
    return;
  }
  
  if (!Array.isArray(roomIds) || roomIds.length === 0) {
    socket.emit('subscribe_response', { 
      status: 'error', 
      message: 'Room IDs must be a non-empty array' 
    });
    return;
  }
  
  const subscribedRooms = [];
  const failedRooms = [];
  
  for (const roomId of roomIds) {
    // Check if user has access to this room
    let hasAccess = isAdmin;
    
    if (!hasAccess) {
      // For regular users, check room access
      hasAccess = await roomService.checkUserRoomAccess(userId, roomId);
    }
    
    if (hasAccess) {
      socket.join(`room_${roomId}`);
      subscribedRooms.push(roomId);
    } else {
      failedRooms.push(roomId);
    }
  }
  
  socket.emit('subscribe_response', { 
    status: 'success', 
    subscribedRooms, 
    failedRooms 
  });
  
  console.log(`Client ${socket.id} subscribed to rooms: ${subscribedRooms.join(', ')}`);
};

/**
 * Handle frontend disconnection
 * @param {Object} socket - Socket instance
 */
const handleFrontendDisconnect = (socket) => {
  console.log('Frontend client disconnected:', socket.id);
  
  // If admin socket
  if (socket.data.role === 'admin') {
    websocketService.unregisterAdminSocket(socket.id);
  }
};

module.exports = {
  setupFrontendHandlers,
  handleFrontendDisconnect
};