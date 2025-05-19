/**
 * @fileoverview Express application setup, configuration, and server initialization.
 * Creates the Express app, HTTP server, Socket.IO instance, and initializes WebSocket handlers.
 * Exports the configured HTTP server instance.
 * @requires express
 * @requires cors
 * @requires path
 * @requires http
 * @requires socket.io
 * @requires ./routes
 * @requires ./utils/logger
 * @requires ./sockets
 * @requires helmet
 */

const express = require("express");
const cors = require("cors");
const path = require("path");
const http = require("http");
const cookieParser = require("cookie-parser");
const { Server } = require("socket.io");
const helmet = require("helmet");
const routes = require("./routes");
const logger = require("./utils/logger");
const { initializeWebSocket } = require("./sockets");

/**
 * @constant {object} corsConfig
 * @description Configuration object for CORS middleware.
 * Reads the client URL from environment variables or defaults to localhost.
 * Defines allowed methods, credentials policy, and allowed headers.
 */
const corsConfig = {
  origin: process.env.CLIENT_URL || "http://localhost:5173",
  methods: ["GET", "POST", "PUT", "DELETE"],
  credentials: true,
  allowedHeaders: [
    "Content-Type",
    "Authorization",
    "X-Client-Type",
    "X-Agent-Id",
  ],
};

/**
 * Creates and configures an Express application instance.
 * Sets up essential middleware including CORS, body parsing, request logging (dev only),
 * static file serving, API routes, health check endpoint, 404 handling, and global error handling.
 * @function createApp
 * @returns {express.Application} The configured Express application instance.
 */
function createApp() {
  const app = express();

  // Apply security headers
  app.use(helmet());

  // Core Middlewares

  app.use(cors(corsConfig));
  app.use(express.json());
  app.use(express.urlencoded({ extended: true }));
  app.use(cookieParser());

  // Development Request Logging
  if (process.env.NODE_ENV === "development") {
    app.use((req, res, next) => {
      const start = Date.now();
      const { method, originalUrl } = req;
      if (
        process.env.NODE_ENV === "development" &&
        method !== "GET" &&
        method !== "POST" &&
        req.body &&
        Object.keys(req.body).length > 0
      ) {
        logger.debug("Request Body:", req.body);
      }
      res.on("finish", () => {
        const duration = Date.now() - start;
        logger.http(
          `${method} ${originalUrl} - Status: ${res.statusCode} - ${duration}ms`
        );
      });
      next();
    });
  }
  // API Routes
  app.use("/api", routes);

  // Health Check Route
  app.get("/health", (req, res) => {
    res.status(200).json({ status: "ok", timestamp: new Date().toISOString() });
  });

  // Static Files
  app.use(express.static(path.join(__dirname, "../public")));

  // Global Error Handler
  app.use((err, req, res, next) => {
    logger.error("Unhandled Application Error:", {
      message: err.message,
      stack: err.stack,
      path: req.path,
      method: req.method,
    });
    const errorResponse = {
      success: false,
      message: err.message || "Internal Server Error",
      ...(process.env.NODE_ENV === "development" && { stack: err.stack }),
    };
    res.status(err.statusCode || 500).json(errorResponse);
  });

  return app;
}

/**
 * @constant {http.Server} httpServer
 * @description The Node.js HTTP server instance created directly from the configured Express app.
 */
const httpServer = http.createServer(createApp());

// Initialize Socket.IO server and WebSocket event handlers directly
initializeWebSocket(
  new Server(httpServer, {
    cors: corsConfig,
  })
);

/**
 * @module app
 * @description Exports the configured Node.js HTTP server instance.
 * This server instance includes the Express application and the attached Socket.IO server.
 * @property {http.Server} httpServer - The initialized HTTP server.
 */
module.exports = {
  httpServer,
};
