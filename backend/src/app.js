/**
 * Express application setup and configuration.
 */
const express = require('express');
const cors = require('cors');
const path = require('path');
const routes = require('./routes');
const logger = require('./utils/logger'); // Assuming a logger utility exists

/**
 * Creates and configures the Express application instance.
 * @returns {express.Application} The configured Express app.
 */
function createApp() {
  const app = express();

  // --- Core Middlewares ---
  app.use(cors()); // Enable Cross-Origin Resource Sharing
  app.use(express.json()); // Parse JSON request bodies
  app.use(express.urlencoded({ extended: true })); // Parse URL-encoded request bodies

  // --- Request Logging Middleware (Development Only) ---
  if (process.env.NODE_ENV === 'development') {
    app.use((req, res, next) => {
      const start = Date.now();
      const { method, originalUrl, ip } = req;
      const userAgent = req.get('User-Agent') || 'N/A';

      logger.info(`--> ${method} ${originalUrl} - IP: ${ip} - Agent: ${userAgent}`);

      if (method !== 'GET' && Object.keys(req.body).length) {
         // Avoid logging sensitive info in production logs if body logging is ever enabled there
         // Consider redacting sensitive fields if necessary
         // logger.debug(`Request Body:`, req.body); // Use debug level
      }

      res.on('finish', () => {
        const duration = Date.now() - start;
        logger.info(`<-- ${method} ${originalUrl} - Status: ${res.statusCode} - ${duration}ms`);
      });

      next();
    });
  }

  // --- Static Files Middleware ---
  // Serves static files (e.g., frontend build) from the 'public' directory
  app.use(express.static(path.join(__dirname, '../public')));

  // --- API Routes ---
  app.use('/api', routes);

  // --- Health Check Route ---
  /**
   * Simple health check endpoint.
   * @route GET /health
   * @returns {object} 200 - JSON object indicating server status.
   */
  app.get('/health', (req, res) => {
    res.status(200).json({ status: 'ok', timestamp: new Date().toISOString() });
  });

  // --- Catch-all for 404 Not Found (after API routes and static files) ---
   app.use((req, res, next) => {
    res.status(404).json({ success: false, message: 'Not Found' });
  });


  // --- Global Error Handling Middleware ---
  /**
   * Handles errors passed via next(err).
   * Logs the error and sends a standardized JSON error response.
   */
  app.use((err, req, res, next) => {
    logger.error('Unhandled Error:', err.stack || err.message || err);

    // Avoid leaking stack trace in production
    const errorResponse = {
      success: false,
      message: err.message || 'Internal Server Error',
      ...(process.env.NODE_ENV === 'development' && { error: err.stack }), // Include stack trace only in dev
    };

    res.status(err.statusCode || 500).json(errorResponse);
  });

  return app;
}

module.exports = createApp(); // Export the created app instance
