import axios from 'axios';

/**
 * API service configuration for backend communication
 * @module api
 */

const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000';
const API_PATH = '/api';

/**
 * Configured axios instance for API communications
 */
const api = axios.create({
  baseURL: BASE_URL + API_PATH,
  headers: {
    'Content-Type': 'application/json'
  },
  withCredentials: true
});

/**
 * Set auth token for API requests
 * @param {string} token - JWT authentication token
 */
const setAuthToken = (token) => {
  if (token) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }
};

/**
 * Remove auth token from API requests
 */
const removeAuthToken = () => {
  delete api.defaults.headers.common['Authorization'];
};

// Queue for storing requests that failed due to 401
let isRefreshing = false;
let failedRequestsQueue = [];

/**
 * Process all requests in the failed queue with the new token
 * @param {string} token - The new JWT token
 */
const processQueue = (token, error = null) => {
  failedRequestsQueue.forEach(prom => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  
  // Reset the queue
  failedRequestsQueue = [];
};

/**
 * Request interceptor to add authentication token to requests
 */
api.interceptors.request.use(
  async (config) => {
    // Import directly from auth.service to avoid circular dependency
    const { default: authService } = await import('./auth.service');
    const user = authService.getCurrentUser();
    
    if (user && user.token) {
      config.headers['Authorization'] = `Bearer ${user.token}`;
    }
    
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Export setAuthToken and removeAuthToken as part of the API
api.setAuthToken = setAuthToken;
api.removeAuthToken = removeAuthToken;

/**
 * Response interceptor to handle errors and extract messages
 * Implements sophisticated token refresh with request queuing
 */
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response) {
      // Extract error message from response
      const errorMessage = error.response.data?.message || 'Unknown server error';
      
      // Handle authentication errors
      if (error.response.status === 401) {
        // Get original request
        const originalRequest = error.config;
        
        // If not a login request and not already a refresh token request
        if (!originalRequest.url.includes('/auth/login') && 
            !originalRequest.url.includes('/auth/refresh-token') && 
            !originalRequest._retry) {
          
          // Mark as retried to prevent infinite loops
          originalRequest._retry = true;
          
          // If token refresh is already in progress, add this request to queue
          if (isRefreshing) {
            try {
              // Wait for the token refresh to complete
              const newToken = await new Promise((resolve, reject) => {
                failedRequestsQueue.push({ resolve, reject });
              });
              
              // Update the request with new token
              originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
              return axios(originalRequest);
            } catch (refreshError) {
              // If refresh failed, let it be handled by the logout logic below
              return Promise.reject(refreshError);
            }
          }
          
          // Start token refresh process
          isRefreshing = true;
          
          try {
            // Import dynamically to avoid circular dependency
            const { default: authService } = await import('./auth.service');
            
            // Attempt to refresh token
            const userData = await authService.refreshToken();
            
            // Update Authorization header
            const newToken = userData.token;
            originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
            
            // Process queued requests with new token
            processQueue(newToken);
            
            // Reset refreshing flag
            isRefreshing = false;
            
            // Retry the original request with new token
            return axios(originalRequest);
          } catch (refreshError) {
            // Refresh token request failed
            
            // Process queue with error to reject all queued requests
            processQueue(null, refreshError);
            
            // Reset refreshing flag
            isRefreshing = false;
            
            // Import auth service to logout
            const { default: authService } = await import('./auth.service');
            authService.logout();
            
            // Redirect to login if not already there
            if (window.location.pathname !== '/login') {
              window.location.href = '/login';
            }
            
            return Promise.reject(refreshError);
          }
        } else if (originalRequest.url.includes('/auth/refresh-token')) {
          // If refresh token request itself failed
          const { default: authService } = await import('./auth.service');
          authService.logout();
          
          // Redirect to login if not already there
          if (window.location.pathname !== '/login') {
            window.location.href = '/login';
          }
        }
      }
      
      // Attach extracted message to the error for easy access
      error.extractedMessage = errorMessage;
    } else if (error.request) {
      // Request was made but no response received
      error.extractedMessage = 'No response from server. Please check your network connection.';
    } else {
      // Something happened in setting up the request
      error.extractedMessage = 'Request configuration error: ' + error.message;
    }
    
    return Promise.reject(error);
  }
);

export default api;