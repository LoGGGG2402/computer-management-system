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

let refreshTokenPromise = null;

/**
 * Handle token refresh
 */
const handleTokenRefresh = async () => {
  try {
    const { default: authService } = await import('./auth.service');
    const userData = await authService.refreshToken();
    
    if (!userData || !userData.token) {
      throw new Error('Token refresh failed');
    }
    
    return userData.token;
  } catch (error) {
    if (error.response?.status === 401 || error.response?.status === 403) {
      const { default: authService } = await import('./auth.service');
      await authService.handleRefreshTokenError();
    }
    throw error;
  } finally {
    refreshTokenPromise = null;
  }
};

/**
 * Request interceptor
 */
api.interceptors.request.use(
  async (config) => {
    const { default: authService } = await import('./auth.service');
    const token = authService.tokenStorage.getToken();
    
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`;
    }
    
    return config;
  },
  (error) => Promise.reject(error)
);

/**
 * Response interceptor
 */
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (!error.response) {
      error.extractedMessage = 'No response from server. Please check your network connection.';
      return Promise.reject(error);
    }

    error.extractedMessage = error.response.data?.message || 'Unknown server error';

    if (error.response.status !== 401) {
      return Promise.reject(error);
    }

    if (originalRequest.url.includes('/auth/refresh-token') || 
        originalRequest.url.includes('/auth/login') ||
        originalRequest._retry) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    try {
      if (!refreshTokenPromise) {
        refreshTokenPromise = handleTokenRefresh();
      }

      const newToken = await refreshTokenPromise;
      originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
      
      return api(originalRequest);
    } catch (error) {
      return Promise.reject(error);
    }
  }
);

export default api;