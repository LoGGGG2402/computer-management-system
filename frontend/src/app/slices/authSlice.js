/**
 * Authentication management slice for Redux store
 * @module authSlice
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import authService from '../services/auth.service';
import api from '../services/api';

/**
 * Async thunk to initialize authentication state
 * Checks for existing token and validates it
 * Attempts token refresh if needed
 * @returns {Promise} Promise resolving to user data or null
 */
export const initializeAuth = createAsyncThunk(
  'auth/initialize',
  async (_, { rejectWithValue }) => {
    try {
      const currentUser = authService.getCurrentUser();
      if (currentUser && currentUser.token) {
        api.setAuthToken(currentUser.token);
        
        if (authService.isTokenExpiringSoon(300)) {
          try {
            await authService.refreshToken();
          } catch {
            console.log('Automatic token refresh failed, continuing with current token');
          }
        }
        
        try {
          const profile = await authService.getProfile();
          return { ...authService.getCurrentUser(), profile };
        } catch (err) {
          console.error('Error verifying token (may be invalid/expired):', err);
          if (err.response && err.response.status === 401) {
            try {
              await authService.refreshToken();
              const profile = await authService.getProfile();
              return { ...authService.getCurrentUser(), profile };
            } catch (refreshErr) {
              console.error('Token refresh failed during initialization:', refreshErr);
              authService.logout();
              api.removeAuthToken();
              return rejectWithValue('Token expired or invalid');
            }
          } else {
            authService.logout();
            api.removeAuthToken();
            return rejectWithValue('Invalid session');
          }
        }
      } else {
        return null;
      }
    } catch (err) {
      console.error('Critical error during authentication initialization:', err);
      authService.logout();
      api.removeAuthToken();
      return rejectWithValue('Authentication failed. Please login again.');
    }
  }
);

/**
 * Async thunk to handle user login
 * @param {Object} params - Login parameters
 * @param {string} params.username - Username
 * @param {string} params.password - Password
 * @returns {Promise} Promise resolving to user data
 */
export const login = createAsyncThunk(
  'auth/login',
  async ({ username, password }, { rejectWithValue }) => {
    try {
      const userData = await authService.login(username, password);
      api.setAuthToken(userData.token);
      const profile = await authService.getProfile();
      const fullUser = { ...userData, profile };
      authService.tokenStorage.setUser(fullUser);
      return fullUser;
    } catch (err) {
      return rejectWithValue(err.response?.data?.message || err.message || 'Login failed');
    }
  }
);

/**
 * Async thunk to handle user logout
 * @returns {Promise} Promise resolving to null
 */
export const logout = createAsyncThunk(
  'auth/logout',
  async (_, { rejectWithValue }) => {
    try {
      await authService.logout();
      api.removeAuthToken();
      return null;
    } catch (err) {
      console.error('Logout error:', err);
      api.removeAuthToken();
      authService.logout();
      return rejectWithValue('Logout failed');
    }
  }
);

/**
 * Async thunk to refresh authentication token
 * @returns {Promise} Promise resolving to updated user data
 */
export const refreshToken = createAsyncThunk(
  'auth/refreshToken',
  async (_, { rejectWithValue }) => {
    try {
      await authService.refreshToken();
      const currentUser = authService.getCurrentUser();
      if (currentUser) {
        return currentUser;
      }
      return rejectWithValue('Không thể làm mới token');
    } catch {
      return rejectWithValue('Làm mới token thất bại');
    }
  }
);

/**
 * Initial state for auth slice
 * @type {Object}
 */
const initialState = {
  user: null,
  loading: true,
  error: null,
};

/**
 * Auth slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    clearError: (state) => {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(initializeAuth.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(initializeAuth.fulfilled, (state, action) => {
        state.loading = false;
        state.user = action.payload;
      })
      .addCase(initializeAuth.rejected, (state, action) => {
        state.loading = false;
        state.user = null;
        state.error = action.payload || 'Khởi tạo xác thực thất bại';
      })
      .addCase(login.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(login.fulfilled, (state, action) => {
        state.loading = false;
        state.user = action.payload;
      })
      .addCase(login.rejected, (state, action) => {
        state.loading = false;
        state.user = null;
        state.error = action.payload;
      })
      .addCase(logout.pending, (state) => {
        state.loading = true;
      })
      .addCase(logout.fulfilled, (state) => {
        state.loading = false;
        state.user = null;
        state.error = null;
      })
      .addCase(logout.rejected, (state, action) => {
        state.loading = false;
        state.user = null;
        state.error = action.payload;
      })
      .addCase(refreshToken.fulfilled, (state, action) => {
        state.user = action.payload;
      })
      .addCase(refreshToken.rejected, (state, action) => {
        state.error = action.payload;
      });
  },
});

/**
 * Selectors for auth state
 */
export const selectAuthUser = (state) => state.auth.user;
export const selectAuthLoading = (state) => state.auth.loading;
export const selectAuthError = (state) => state.auth.error;
export const selectIsAuthenticated = (state) => !!state.auth.user;
export const selectUserRole = (state) => state.auth.user?.role;

export const { clearError } = authSlice.actions;
export default authSlice.reducer; 