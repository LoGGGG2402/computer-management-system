/**
 * Admin management slice for Redux store
 * @module adminSlice
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import adminService from '../services/admin.service';

/**
 * Async thunk to fetch system statistics
 * @returns {Promise} Promise resolving to system stats data
 */
export const fetchSystemStats = createAsyncThunk(
  'admin/fetchSystemStats',
  async (_, { rejectWithValue }) => {
    try {
      return await adminService.getSystemStats();
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch agent versions
 * @returns {Promise} Promise resolving to agent versions data
 */
export const fetchAgentVersions = createAsyncThunk(
  'admin/fetchAgentVersions',
  async (_, { rejectWithValue }) => {
    try {
      return await adminService.getAgentVersions();
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to upload new agent version
 * @param {FormData} formData - Form data containing agent package
 * @returns {Promise} Promise resolving to uploaded agent version data
 */
export const uploadAgentVersion = createAsyncThunk(
  'admin/uploadAgentVersion',
  async (formData, { rejectWithValue }) => {
    try {
      return await adminService.uploadAgentPackage(formData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to update agent version stability status
 * @param {Object} params - Update parameters
 * @param {string|number} params.versionId - Version ID to update
 * @param {boolean} params.isStable - New stability status
 * @returns {Promise} Promise resolving to updated agent version data
 */
export const updateAgentVersionStability = createAsyncThunk(
  'admin/updateAgentVersionStability',
  async ({ versionId, isStable }, { rejectWithValue }) => {
    try {
      return await adminService.updateAgentVersionStability(versionId, isStable);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Initial state for admin slice
 * @type {Object}
 */
const initialState = {
  systemStats: null,
  agentVersions: [],
  loading: false,
  error: null,
};

/**
 * Admin slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const adminSlice = createSlice({
  name: 'admin',
  initialState,
  reducers: {
    clearAdminError: (state) => {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchSystemStats.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchSystemStats.fulfilled, (state, action) => {
        state.loading = false;
        state.systemStats = action.payload;
      })
      .addCase(fetchSystemStats.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(fetchAgentVersions.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchAgentVersions.fulfilled, (state, action) => {
        state.loading = false;
        state.agentVersions = action.payload;
      })
      .addCase(fetchAgentVersions.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(uploadAgentVersion.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(uploadAgentVersion.fulfilled, (state, action) => {
        state.loading = false;
        state.agentVersions.push(action.payload);
      })
      .addCase(uploadAgentVersion.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(updateAgentVersionStability.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(updateAgentVersionStability.fulfilled, (state, action) => {
        state.loading = false;
        const index = state.agentVersions.findIndex(v => v.id === action.payload.id);
        if (index !== -1) {
          state.agentVersions[index] = action.payload;
        }
      })
      .addCase(updateAgentVersionStability.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

/**
 * Selectors for admin state
 */
export const selectSystemStats = (state) => state.admin.systemStats;
export const selectAgentVersions = (state) => state.admin.agentVersions;
export const selectAdminLoading = (state) => state.admin.loading;
export const selectAdminError = (state) => state.admin.error;

export const { clearAdminError } = adminSlice.actions;
export default adminSlice.reducer; 