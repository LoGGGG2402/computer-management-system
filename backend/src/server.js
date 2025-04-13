/**
 * Main server startup script.
 * Initializes HTTP server, Socket.IO, database connection, and starts listening.
 */
const http = require('http');
const { Server } = require('socket.io');
const dotenv = require('dotenv');
const app = require('./app'); // Import the app instance directly
const db = require('./database/models'); // Assuming Sequelize models index
const { initializeWebSocket } = require('./sockets'); // WebSocket initializer

// Load environment variables from .env file
dotenv.config();

/**
 * Asynchronously starts the server.
 * Connects to the database, initializes Socket.IO, and starts the HTTP server.
 */
async function startServer() {
  try {
    console.info('Starting server initialization...');

    // --- Database Connection ---
    console.info('Attempting to connect to the database...');
    await db.sequelize.authenticate(); // Verify connection details
    // Optional: Sync models if needed (use with caution in production)
    // await db.sequelize.sync({ alter: process.env.NODE_ENV === 'development' });
    console.info('Database connection established successfully.');

    // --- Create HTTP Server ---
    const httpServer = http.createServer(app);

    // --- Initialize Socket.IO ---
    const io = new Server(httpServer, {
      cors: {
        origin: process.env.CLIENT_URL || 'http://localhost:5173', 
        methods: ['GET', 'POST'],
      },
    });
    console.info(`Socket.IO initialized with CORS origin: ${process.env.CLIENT_URL || 'http://localhost:5173'}`);

    // --- Initialize WebSocket Handlers ---
    initializeWebSocket(io); // Pass the io instance to the WebSocket setup function
    console.info('WebSocket handlers initialized.');

    // --- Set Port ---
    const PORT = process.env.PORT || 3000;
    if (!PORT) {
        console.warn('PORT environment variable not set, defaulting to 3000.');
    }

    // --- Start Listening ---
    httpServer.listen(PORT, () => {
      console.info(`Server is running and listening on port ${PORT}`);
      console.info(`Access API at http://localhost:${PORT}`);
      if (process.env.CLIENT_URL) {
          console.info(`Frontend expected at ${process.env.CLIENT_URL}`);
      }
    });

    // --- Graceful Shutdown Handling (Optional but Recommended) ---
    const signals = ['SIGINT', 'SIGTERM'];
    signals.forEach(signal => {
        process.on(signal, async () => {
            console.info(`Received ${signal}. Shutting down gracefully...`);
            httpServer.close(async () => {
                console.info('HTTP server closed.');
                try {
                    await db.sequelize.close();
                    console.info('Database connection closed.');
                } catch (dbError) {
                    console.error('Error closing database connection:', dbError);
                } finally {
                    process.exit(0); // Exit successfully
                }
            });

            // Force close after a timeout if graceful shutdown fails
            setTimeout(() => {
                console.error('Graceful shutdown timed out. Forcing exit.');
                process.exit(1);
            }, 10000); // 10 seconds timeout
        });
    });


  } catch (error) {
    console.error('Failed to start server:', error.stack || error.message || error);
    process.exit(1); // Exit with error code
  }
}

// --- Execute Server Start ---
startServer();
