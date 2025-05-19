/**
 * Centralized logging utility for the application.
 * Uses Winston for consistent, configurable logging across the application.
 */
const winston = require("winston");
const path = require("path");
const fs = require("fs");

// Create logs directory if it doesn't exist
const logDir = path.join(__dirname, "../../logs");
if (!fs.existsSync(logDir)) {
  fs.mkdirSync(logDir);
}

// Define log levels and colors
const levels = {
  error: 0,
  warn: 1,
  info: 2,
  http: 3,
  debug: 4,
};

const level = () => {
  const env = process.env.NODE_ENV || "development";
  return env === "development" ? "debug" : "info";
};

const colors = {
  error: "red",
  warn: "yellow",
  info: "green",
  http: "magenta",
  debug: "blue",
};

winston.addColors(colors);

const consoleFormat = winston.format.combine(
  winston.format.timestamp({ format: "YYYY-MM-DD HH:mm:ss:ms" }),
  winston.format.colorize({ all: true }),
  winston.format.printf((info) => {
    const { timestamp, level, message, ...meta } = info;
    const metaStr = Object.keys(meta).length
      ? `\n${JSON.stringify(meta, null, 2)}`
      : "";
    return `${timestamp} ${level}: ${message}${metaStr}`;
  })
);

const fileFormat = winston.format.combine(
  winston.format.timestamp({ format: "YYYY-MM-DD HH:mm:ss:ms" }),
  winston.format.json()
);

const logger = winston.createLogger({
  level: level(),
  levels,
  format: winston.format.json(),
  transports: [
    new winston.transports.Console({
      format: consoleFormat,
      level: "http",
    }),
    new winston.transports.File({
      filename: path.join(logDir, "error.log"),
      level: "error",
      format: fileFormat,
      maxsize: 5242880,
      maxFiles: 5,
    }),
    new winston.transports.File({
      filename: path.join(logDir, "combined.log"),
      format: fileFormat,
      maxsize: 5242880,
      maxFiles: 5,
    }),
    new winston.transports.File({
      filename: path.join(logDir, "debug.log"),
      level: "debug",
      format: fileFormat,
      maxsize: 5242880,
      maxFiles: 5,
    }),
  ],
});

module.exports = {
  error: (message, meta = {}) => {
    logger.error(message, { ...meta });
  },
  warn: (message, meta = {}) => {
    logger.warn(message, { ...meta });
  },
  info: (message, meta = {}) => {
    logger.info(message, { ...meta });
  },
  http: (message, meta = {}) => {
    logger.http(message, { ...meta });
  },
  debug: (message, meta = {}) => {
    logger.debug(message, { ...meta });
  },
};
