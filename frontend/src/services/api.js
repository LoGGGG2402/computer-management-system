import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000/api';

/**
 * Create a configured axios instance for API requests
 */
const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json'
  }
});

/**
 * Set auth token for API requests
 * @param {string} token - JWT token
 */
const setAuthToken = (token) => {
  if (token) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  } else {
    delete api.defaults.headers.common['Authorization'];
  }
};

/**
 * Remove auth token from API requests
 */
const removeAuthToken = () => {
  delete api.defaults.headers.common['Authorization'];
};

/**
 * Extract error message from API error response
 * @param {Error} error - Axios error object
 * @returns {string} - Extracted error message
 */
const extractErrorMessage = (error) => {
  // Default error message
  let errorMessage = 'An error occurred. Please try again.';
  
  if (error.response) {
    // The request was made and the server responded with a status code
    // that falls out of the range of 2xx
    const { data, status } = error.response;
    
    // Try to extract message from various response formats
    if (data) {
      if (data.message) {
        errorMessage = data.message;
      } else if (data.error) {
        errorMessage = data.error;
      } else if (data.status === 'error' && data.message) {
        errorMessage = data.message;
      } else if (typeof data === 'string') {
        errorMessage = data;
      }
    }
    
    // Add status code for non-401 errors (401 handled separately)
    if (status !== 401) {
      errorMessage = `${errorMessage} (Status: ${status})`;
    }
  } else if (error.request) {
    // The request was made but no response was received
    errorMessage = 'No response from server. Please check your connection.';
  } else {
    // Something happened in setting up the request
    errorMessage = error.message || errorMessage;
  }
  
  return errorMessage;
};

// Add response interceptor for handling errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    // Extract error message
    const errorMessage = extractErrorMessage(error);
    
    // Handle 401 Unauthorized errors (e.g., token expired)
    if (error.response && error.response.status === 401) {
      // Clear local storage and redirect to login
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    
    // Enhance error object with extracted message for easier handling
    error.extractedMessage = errorMessage;
    
    return Promise.reject(error);
  }
);

export default {
  api,
  setAuthToken,
  removeAuthToken,
  extractErrorMessage,
  get: api.get,
  post: api.post,
  put: api.put,
  delete: api.delete
};