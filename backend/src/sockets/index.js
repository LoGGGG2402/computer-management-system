/**
 * Socket.IO event handlers
 * @param {Object} io - Socket.IO server instance
 */
module.exports = (io) => {
  // Connection event
  io.on('connection', (socket) => {
    console.log('New client connected:', socket.id);
    
    // Join room event
    socket.on('join_room', (roomId) => {
      socket.join(roomId);
      console.log(`Client ${socket.id} joined room: ${roomId}`);
    });
    
    // Leave room event
    socket.on('leave_room', (roomId) => {
      socket.leave(roomId);
      console.log(`Client ${socket.id} left room: ${roomId}`);
    });
    
    // Message event
    socket.on('message', (data) => {
      console.log('Message received:', data);
      io.to(data.roomId).emit('message', data);
    });
    
    // Disconnection event
    socket.on('disconnect', () => {
      console.log('Client disconnected:', socket.id);
    });
  });
};