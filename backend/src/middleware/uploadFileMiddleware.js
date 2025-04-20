const multer = require('multer');
const path = require('path');
const fs = require('fs');
const logger = require('../utils/logger'); // Assuming you have a logger utility at this path

// Ensure upload directories exist
const UPLOAD_DIR = path.join(__dirname, '../../uploads');
const AGENT_PACKAGES_DIR = path.join(UPLOAD_DIR, 'agent-packages');

// Create directories if they don't exist
[UPLOAD_DIR, AGENT_PACKAGES_DIR].forEach(dir => {
  if (!fs.existsSync(dir)) {
    try {
      fs.mkdirSync(dir, { recursive: true });
      logger.info(`Created directory: ${dir}`);
    } catch (error) {
      logger.error(`Failed to create directory ${dir}:`, { error: error.message });
    }
  }
});

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

// Configure storage for agent packages
const agentPackageStorage = multer.diskStorage({
  destination: (req, file, cb) => {
    cb(null, AGENT_PACKAGES_DIR);
  },
  filename: (req, file, cb) => {
    // Generate filename based on version and current timestamp
    const version = req.body.version ? req.body.version.replace(/[^a-zA-Z0-9.-]/g, '_') : '';
    const timestamp = Date.now();
    const ext = path.extname(file.originalname) || '.zip';
    const filename = `agent_${version}_${timestamp}${ext}`;
    
    cb(null, filename);
  }
});

// File filter - only accept zip files
const agentPackageFileFilter = (req, file, cb) => {
  const allowedTypes = ['.zip', '.gz', '.tar'];
  const ext = path.extname(file.originalname).toLowerCase();
  
  if (allowedTypes.includes(ext)) {
    cb(null, true);
  } else {
    cb(new Error('Only archive files (.zip, .gz, .tar) are allowed'), false);
  }
};

// Create the multer instance with limits for agent packages
const agentPackageUpload = multer({
  storage: agentPackageStorage,
  fileFilter: agentPackageFileFilter,
  limits: {
    fileSize: 50 * 1024 * 1024, // 50 MB max file size
    files: 1 // Maximum one file per request
  }
});

// Single file upload middleware for agent packages
const uploadAgentPackage = (req, res, next) => {
  const uploadMiddleware = agentPackageUpload.single('package');
  
  uploadMiddleware(req, res, (err) => {
    if (err) {
      if (err instanceof multer.MulterError) {
        // Multer-specific errors
        logger.error('File upload error (Multer):', { error: err.message });
        return res.status(400).json({
          status: 'error',
          message: `Upload failed: ${err.message}`
        });
      } else {
        // General errors
        logger.error('File upload error:', { error: err.message });
        return res.status(400).json({
          status: 'error',
          message: err.message
        });
      }
    } 
    
    // No file uploaded
    if (!req.file) {
      logger.warn('No file uploaded in request');
      return res.status(400).json({
        status: 'error',
        message: 'No package file uploaded'
      });
    }
    
    // File uploaded successfully
    logger.info(`Agent package uploaded successfully: ${req.file.filename} (${req.file.size} bytes)`);
    next();
  });
};

module.exports = {
  uploadFileMiddleware,
  uploadAgentPackage,
  AGENT_PACKAGES_DIR
};
