import axios from 'axios';

/**
 * API service configuration for backend communication
 * @module api
 */

const BASE_URL = import.meta.env.VITE_API_URL + '/api' || 'http://localhost:3000/api';

/**
 * Configured axios instance for API communications
 */
const api = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
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

/**
 * Request interceptor to add authentication token to requests
 */
api.interceptors.request.use(
  (config) => {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        const user = JSON.parse(userStr);
        if (user && user.token) {
          config.headers['Authorization'] = `Bearer ${user.token}`;
        }
      } catch (error) {
        console.error('Error parsing user data:', error);
      }
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
 */
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      // Extract error message from response
      const errorMessage = error.response.data?.message || 'Unknown server error';
      
      // Handle authentication errors
      if (error.response.status === 401) {
        // If not a login request, clear local storage
        if (!error.config.url.includes('/auth/login')) {
          localStorage.removeItem('user');
          
          // Redirect to login page if not already there
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