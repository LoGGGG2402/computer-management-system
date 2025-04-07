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

// Add response interceptor for handling token expiration
api.interceptors.response.use(
  (response) => response,
  (error) => {
    // Handle 401 Unauthorized errors (e.g., token expired)
    if (error.response && error.response.status === 401) {
      // Clear local storage and redirect to login
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default {
  api,
  setAuthToken,
  removeAuthToken,
  get: api.get,
  post: api.post,
  put: api.put,
  delete: api.delete
};