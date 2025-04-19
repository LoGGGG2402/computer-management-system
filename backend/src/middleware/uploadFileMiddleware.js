const multer = require('multer');
const path = require('path');
const fs = require('fs');
const logger = require('../utils/logger'); // Assuming you have a logger utility at this path

/**
 * Creates middleware to handle single file uploads with configuration options.
 *
 * @param {Object} options - Configuration options for the middleware.
 * @param {string} [options.fieldName='file'] - The name of the form field containing the file.
 * @param {string} [options.destination='./uploads'] - The directory to store uploaded files.
 * @param {string[]} [options.allowedTypes] - Array of allowed MIME types (e.g., ['image/jpeg', 'image/png']). If not provided or empty, all types are accepted.
 * @param {number} [options.maxSize=5 * 1024 * 1024] - Maximum allowed file size in bytes. Defaults to 5MB.
 * @returns {Function} - Express middleware function.
 */
const uploadFileMiddleware = (options = {}) => {
  const fieldName = options.fieldName || 'file';
  const destination = options.destination || '../../uploads';
  const allowedTypes = options.allowedTypes || null;
  const maxSize = options.maxSize || 5 * 1024 * 1024;

  try {
    if (!fs.existsSync(destination)) {
      fs.mkdirSync(destination, { recursive: true });
      logger.info(`Created upload directory: ${destination}`);
    }
  } catch (err) {
    logger.error(`Failed to create upload directory: ${destination}`, { error: err.message, stack: err.stack });
    return (req, res, next) => {
      res.status(500).json({ status: 'error', message: 'Could not initialize upload directory.' });
    };
  }

  const storage = multer.diskStorage({
    destination: (req, file, cb) => {
      cb(null, destination);
    },
    filename: (req, file, cb) => {
      const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
      const extension = path.extname(file.originalname);
      const newFilename = `${file.fieldname}-${uniqueSuffix}${extension}`;
      logger.debug(`Generating filename for upload: ${newFilename}`, { originalName: file.originalname });
      cb(null, newFilename);
    }
  });

  const fileFilter = (req, file, cb) => {
    if (!allowedTypes || allowedTypes.length === 0) {
       logger.debug(`File type check skipped for ${file.originalname} (all types allowed)`);
      return cb(null, true);
    }

    if (allowedTypes.includes(file.mimetype)) {
      cb(null, true);
    } else {
      logger.warn(`Invalid file type rejected: ${file.mimetype} for ${file.originalname}`, {
        allowedTypes,
        endpoint: `${req.method} ${req.originalUrl}`,
        ip: req.ip
      });
      const error = new Error(`Invalid file type. Only ${allowedTypes.join(', ')} are allowed.`);
      error.code = 'INVALID_FILE_TYPE';
      cb(error, false);
    }
  };

  const upload = multer({
    storage: storage,
    fileFilter: fileFilter,
    limits: {
      fileSize: maxSize
    }
  });

  const singleUploadMiddleware = upload.single(fieldName);

  return (req, res, next) => {
    singleUploadMiddleware(req, res, (err) => {
      if (err) {
        if (err instanceof multer.MulterError) {
          logger.warn(`Multer error during upload: ${err.message}`, {
            code: err.code,
            field: err.field,
            endpoint: `${req.method} ${req.originalUrl}`,
            ip: req.ip
          });
          if (err.code === 'LIMIT_FILE_SIZE') {
            return res.status(400).json({ status: 'error', message: `File too large. Maximum size allowed is ${maxSize / 1024 / 1024}MB.` });
          }
          if (err.code === 'LIMIT_UNEXPECTED_FILE') {
             return res.status(400).json({ status: 'error', message: `Unexpected file field '${err.field}'. Expected a single file using field name '${fieldName}'.` });
          }
          return res.status(400).json({ status: 'error', message: `Upload error: ${err.message}` });
        } else if (err && err.code === 'INVALID_FILE_TYPE') {
          logger.warn(`Invalid file type error caught: ${err.message}`, {
            endpoint: `${req.method} ${req.originalUrl}`,
            ip: req.ip
          });
          return res.status(400).json({ status: 'error', message: err.message });
        } else if (err) {
          logger.error('Unknown error during file upload:', {
            error: err.message,
            stack: err.stack,
            endpoint: `${req.method} ${req.originalUrl}`,
            ip: req.ip
          });
          return res.status(500).json({ status: 'error', message: 'An unexpected error occurred during file upload.' });
        }
      }

      if (req.file) {
        logger.info(`File uploaded successfully: ${req.file.filename}`, {
             size: req.file.size,
             mimetype: req.file.mimetype,
             destination: req.file.destination,
             endpoint: `${req.method} ${req.originalUrl}`,
             ip: req.ip
            });
      } else {
          logger.debug('No file was uploaded for the request.', { endpoint: `${req.method} ${req.originalUrl}`, ip: req.ip });
      }

      next();
    });
  };
};

module.exports = { uploadFileMiddleware };
