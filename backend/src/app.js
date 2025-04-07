const express = require('express');
const cors = require('cors');
const path = require('path');
const routes = require('./routes');

// Initialize Express app
const app = express();

// Middlewares
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// API Logger Middleware (only for development)
if (process.env.NODE_ENV === 'development') {
  app.use((req, res, next) => {
    const start = Date.now();
    const { method, originalUrl } = req;
    
    console.log(`[API] ${method} ${originalUrl} - Request received`);
    
    if (method !== 'GET' && Object.keys(req.body).length) {
      console.log(`[API] Request Body:`, JSON.stringify(req.body, null, 2));
    }
    
    // Log response after it's sent
    res.on('finish', () => {
      const duration = Date.now() - start;
      console.log(`[API] ${method} ${originalUrl} - Status: ${res.statusCode} - ${duration}ms`);
    });
    
    next();
  });
}

// Static files
app.use(express.static(path.join(__dirname, '../public')));

// API Routes
app.use('/api', routes);

// Health check route
app.get('/health', (req, res) => {
  res.status(200).json({ status: 'ok', message: 'Server is running' });
});

// Error handling middleware
app.use((err, req, res, next) => {
  console.error(err.stack);
  res.status(err.statusCode || 500).json({
    success: false,
    message: err.message || 'Internal Server Error',
    error: process.env.NODE_ENV === 'development' ? err : {}
  });
});

module.exports = app;