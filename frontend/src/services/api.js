import axios from 'axios';
// Using dynamic import for auth.service to avoid circular dependency issues during initialization
// and to ensure authService is only imported when needed.

const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000';
const API_PATH = '/api';

/**
 * Configure axios instance for backend API communication.
 * @module api
 */
const api = axios.create({
  baseURL: BASE_URL + API_PATH,
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true, // Important for sending and receiving cookies (e.g., refreshToken)
});

let refreshTokenPromise = null;

/**
 * Handle access token refresh.
 * Prevents multiple simultaneous token refresh requests.
 * @async
 * @returns {Promise<string>} New access token.
 * @throws {Error} If token refresh fails or causes an error.
 */
const handleTokenRefresh = async () => {
  console.log('[API handleTokenRefresh] Attempting to refresh token...');
  try {
    // Dynamic import of authService here to avoid circular dependencies
    const { default: authService } = await import('./auth.service');
    const userData = await authService.refreshToken();

    if (!userData || !userData.token) {
      console.error('[API handleTokenRefresh] Token refresh failed to return a new token from authService.');
      throw new Error('Token refresh failed to return a new token.');
    }
    console.log('[API handleTokenRefresh] Token refreshed successfully by authService.');
    return userData.token;
  } catch (error) {
    console.error('[API handleTokenRefresh] Error during token refresh process:', error.message, error.response?.status, error.response?.data);
    throw error;
  } finally {
    refreshTokenPromise = null;
    console.log('[API handleTokenRefresh] refreshTokenPromise reset.');
  }
};

/**
 * Request interceptor.
 * Automatically attaches Authorization header with access token if available.
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
  (error) => {
    console.error('[API Request Interceptor] Error:', error);
    return Promise.reject(error);
  }
);

/**
 * Response interceptor.
 * Handles errors, especially 401 Unauthorized errors and 403 Forbidden errors for automatic token refresh.
 */
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    console.log('[API Response Interceptor] Encountered response error.', {
      url: originalRequest.url,
      status: error.response?.status,
      data: error.response?.data,
      isRetry: originalRequest._retry,
      method: originalRequest.method,
    });

    // Handle network errors or server not responding
    if (!error.response) {
      error.extractedMessage = 'No response from server. Please check your network connection.';
      console.warn('[API Response Interceptor] Network error or no response from server.');
      return Promise.reject(error);
    }

    // Extract error message from server response if available
    error.extractedMessage = error.response.data?.message || error.message || 'Unknown server error.';

    // Conditions to bypass token refresh
    const bypassRefreshConditions =
      originalRequest.url.includes('/auth/refresh-token') ||
      originalRequest.url.includes('/auth/login') ||
      originalRequest.url.includes('/auth/logout') ||
      originalRequest._retry;

    if (!(error.response.status == 401 || error.response.status == 403) || bypassRefreshConditions) {
      console.log('[API Response Interceptor] Bypassing token refresh. Details:', {
        status: error.response.status,
        isRefreshOrLoginUrl: originalRequest.url.includes('/auth/refresh-token') || originalRequest.url.includes('/auth/login'),
        isRetry: originalRequest._retry,
      });

      // If error is 401 or 403 from /auth/refresh-token endpoint itself,
      // it means refresh token is invalid. Call handleRefreshTokenError.
      if ((error.response.status === 401 || error.response.status === 403) && originalRequest.url.includes('/auth/refresh-token')) {
        console.warn('[API Response Interceptor] Error from /auth/refresh-token itself. Refresh token might be invalid. Logging out.');
        const { default: authService } = await import('./auth.service');
        await authService.handleRefreshTokenError();
      }
      return Promise.reject(error);
    }

    // If we reach here, error is 401 and not in bypass conditions. Proceed with token refresh.
    console.log('[API Response Interceptor] Status is 401 and not a bypass condition. Attempting token refresh for original request to:', originalRequest.url);
    originalRequest._retry = true;

    try {
      if (!refreshTokenPromise) {
        console.log('[API Response Interceptor] No existing refreshTokenPromise. Creating new one.');
        refreshTokenPromise = handleTokenRefresh();
      } else {
        console.log('[API Response Interceptor] Waiting for existing refreshTokenPromise.');
      }
      const newToken = await refreshTokenPromise;
      console.log('[API Response Interceptor] New token obtained. Retrying original request to:', originalRequest.url);

      originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
      return api(originalRequest);
    } catch (refreshError) {
      console.error('[API Response Interceptor] Token refresh attempt failed. Error will be propagated to original caller. Refresh error:', refreshError.message);
      return Promise.reject(refreshError);
    }
  }
);

export default api;
