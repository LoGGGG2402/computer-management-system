/**
 * Main server startup script.
 * Initializes HTTP server, Socket.IO, database connection, and starts listening.
 */
const http = require('http');
const { Server } = require('socket.io');
const dotenv = require('dotenv');
const createApp = require('./app'); // Import the app factory function
const db = require('./database/models'); // Assuming Sequelize models index
const { initializeWebSocket } = require('./sockets'); // WebSocket initializer
const logger = require('./utils/logger'); // Assuming a logger utility exists

// Load environment variables from .env file
dotenv.config();

/**
 * Asynchronously starts the server.
 * Connects to the database, initializes Socket.IO, and starts the HTTP server.
 */
async function startServer() {
  try {
    logger.info('Starting server initialization...');

    // --- Database Connection ---
    logger.info('Attempting to connect to the database...');
    await db.sequelize.authenticate(); // Verify connection details
    // Optional: Sync models if needed (use with caution in production)
    // await db.sequelize.sync({ alter: process.env.NODE_ENV === 'development' });
    logger.info('Database connection established successfully.');

    // --- Create Express App ---
    const app = createApp(); // Create the Express app instance

    // --- Create HTTP Server ---
    const httpServer = http.createServer(app);

    // --- Initialize Socket.IO ---
    const io = new Server(httpServer, {
      cors: {
        origin: process.env.CLIENT_URL || 'http://localhost:5173', 
        methods: ['GET', 'POST'],
      },
    });
    logger.info(`Socket.IO initialized with CORS origin: ${process.env.CLIENT_URL || 'http://localhost:5173'}`);

    // --- Initialize WebSocket Handlers ---
    initializeWebSocket(io); // Pass the io instance to the WebSocket setup function
    logger.info('WebSocket handlers initialized.');

    // --- Set Port ---
    const PORT = process.env.PORT || 3000;
    if (!PORT) {
        logger.warn('PORT environment variable not set, defaulting to 3000.');
    }

    // --- Start Listening ---
    httpServer.listen(PORT, () => {
      logger.info(`Server is running and listening on port ${PORT}`);
      logger.info(`Access API at http://localhost:${PORT}`);
      if (process.env.CLIENT_URL) {
          logger.info(`Frontend expected at ${process.env.CLIENT_URL}`);
      }
    });

    // --- Graceful Shutdown Handling (Optional but Recommended) ---
    const signals = ['SIGINT', 'SIGTERM'];
    signals.forEach(signal => {
        process.on(signal, async () => {
            logger.info(`Received ${signal}. Shutting down gracefully...`);
            httpServer.close(async () => {
                logger.info('HTTP server closed.');
                try {
                    await db.sequelize.close();
                    logger.info('Database connection closed.');
                } catch (dbError) {
                    logger.error('Error closing database connection:', dbError);
                } finally {
                    process.exit(0); // Exit successfully
                }
            });

            // Force close after a timeout if graceful shutdown fails
            setTimeout(() => {
                logger.error('Graceful shutdown timed out. Forcing exit.');
                process.exit(1);
            }, 10000); // 10 seconds timeout
        });
    });


  } catch (error) {
    logger.error('Failed to start server:', error.stack || error.message || error);
    process.exit(1); // Exit with error code
  }
}

// --- Execute Server Start ---
startServer();
