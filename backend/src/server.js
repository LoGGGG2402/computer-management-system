/**
 * @fileoverview Main server startup script.
 * Loads environment variables, connects to the database, and starts the HTTP server
 * imported from app.js. Handles graceful shutdown.
 * @requires dotenv
 * @requires ./app
 * @requires ./database/models
 * @requires ./utils/logger
 */

const dotenv = require('dotenv');
const { httpServer } = require('./app');
const db = require('./database/models');
const logger = require('./utils/logger');
const { initializeTokenCleanupScheduler } = require('./scheduler');

dotenv.config();

/**
 * Asynchronously starts the application server.
 * Connects to the database, determines the listening port, starts the HTTP server,
 * and sets up graceful shutdown listeners.
 * @async
 * @function startServer
 * @throws {Error} If database connection fails or another critical startup error occurs.
 */
async function startServer() {
  try {
    logger.info('Server initialization sequence started...');

    // 1. Database Connection
    logger.info('Attempting database connection...');
    await db.sequelize.authenticate();
    logger.info('Database connection successful.');

    // 2. Determine Port
    const PORT = process.env.PORT || 3000;

    // 3. Start HTTP Server (imported from app.js)
    httpServer.listen(PORT, () => {
      logger.info(`Server listening on port ${PORT}`);
      if (process.env.CLIENT_URL) {
          logger.info(`Expected frontend client URL: ${process.env.CLIENT_URL}`);
      }
      
      // Initialize the token cleanup scheduler using node-cron
      initializeTokenCleanupScheduler();
    });

    // 4. Graceful Shutdown Handling
    const signals = ['SIGINT', 'SIGTERM'];
    signals.forEach(signal => {
        process.on(signal, async () => {
            logger.info(`Received ${signal}. Initiating graceful shutdown...`);
            httpServer.close(async () => {
                logger.info('HTTP server closed.');
                try {
                    await db.sequelize.close();
                    logger.info('Database connection closed.');
                } catch (dbError) {
                    logger.error('Error closing database connection:', { error: dbError.message });
                } finally {
                    logger.info('Graceful shutdown complete.');
                    process.exit(0);
                }
            });
            setTimeout(() => {
                logger.error('Graceful shutdown timed out after 10 seconds. Forcing exit.');
                process.exit(1);
            }, 10000);
        });
    });

  } catch (error) {
    logger.error('Fatal error during server startup:', {
        message: error.message,
        stack: error.stack
    });
    process.exit(1);
  }
}

startServer();
