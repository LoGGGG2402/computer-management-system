/**
 * Computer management slice for Redux store
 * @module computerSlice
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import computerService from '../services/computer.service';

/**
 * Async thunk to fetch all computers with optional filters
 * @param {Object} filters - Optional filters for computer query
 * @returns {Promise} Promise resolving to computer data
 */
export const fetchComputers = createAsyncThunk(
  'computers/fetchAll',
  async (filters = {}, { rejectWithValue }) => {
    try {
      return await computerService.getAllComputers(filters);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch a single computer by ID
 * @param {string|number} id - Computer ID to fetch
 * @returns {Promise} Promise resolving to computer data
 */
export const fetchComputerById = createAsyncThunk(
  'computers/fetchById',
  async (id, { rejectWithValue }) => {
    try {
      return await computerService.getComputerById(id);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to delete a computer
 * @param {string|number} id - Computer ID to delete
 * @returns {Promise} Promise resolving to deleted computer ID
 */
export const deleteComputer = createAsyncThunk(
  'computers/delete',
  async (id, { rejectWithValue }) => {
    try {
      await computerService.deleteComputer(id);
      return id;
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch errors for a computer
 * @param {string|number} id - Computer ID to fetch errors for
 * @returns {Promise} Promise resolving to computer errors
 */
export const fetchComputerErrors = createAsyncThunk(
  'computers/fetchErrors',
  async (id, { rejectWithValue }) => {
    try {
      return {
        id,
        errors: await computerService.getComputerErrors(id)
      };
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to report a new error for a computer
 * @param {Object} params - Report parameters
 * @param {string|number} params.id - Computer ID
 * @param {Object} params.errorData - Error data to report
 * @returns {Promise} Promise resolving to reported error
 */
export const reportComputerError = createAsyncThunk(
  'computers/reportError',
  async ({ id, errorData }, { rejectWithValue }) => {
    try {
      return await computerService.reportComputerError(id, errorData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to resolve a computer error
 * @param {Object} params - Resolution parameters
 * @param {string|number} params.computerId - Computer ID
 * @param {string|number} params.errorId - Error ID to resolve
 * @param {Object} params.data - Resolution data
 * @returns {Promise} Promise resolving to resolved error
 */
export const resolveComputerError = createAsyncThunk(
  'computers/resolveError',
  async ({ computerId, errorId, data }, { rejectWithValue }) => {
    try {
      return await computerService.resolveComputerError(computerId, errorId, data);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Initial state for computer slice
 * @type {Object}
 */
const initialState = {
  computers: [],
  selectedComputer: null,
  computerErrors: {},
  loading: false,
  error: null,
  pagination: {
    total: 0,
    currentPage: 1,
    totalPages: 1
  }
};

/**
 * Computer slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const computerSlice = createSlice({
  name: 'computers',
  initialState,
  reducers: {
    clearComputerError: (state) => {
      state.error = null;
    },
    setCurrentPage: (state, action) => {
      state.pagination.currentPage = action.payload;
    }
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchComputers.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchComputers.fulfilled, (state, action) => {
        state.loading = false;
        state.computers = action.payload.computers;
        state.pagination.total = action.payload.total;
        state.pagination.currentPage = action.payload.currentPage;
        state.pagination.totalPages = action.payload.totalPages;
      })
      .addCase(fetchComputers.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(fetchComputerById.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchComputerById.fulfilled, (state, action) => {
        state.loading = false;
        state.selectedComputer = action.payload;
      })
      .addCase(fetchComputerById.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(deleteComputer.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(deleteComputer.fulfilled, (state, action) => {
        state.loading = false;
        state.computers = state.computers.filter(computer => computer.id !== action.payload);
        if (state.selectedComputer && state.selectedComputer.id === action.payload) {
          state.selectedComputer = null;
        }
      })
      .addCase(deleteComputer.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(fetchComputerErrors.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchComputerErrors.fulfilled, (state, action) => {
        state.loading = false;
        state.computerErrors[action.payload.id] = action.payload.errors;
      })
      .addCase(fetchComputerErrors.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(reportComputerError.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(reportComputerError.fulfilled, (state, action) => {
        state.loading = false;
        const computerId = action.payload.computerId;
        if (state.computerErrors[computerId]) {
          state.computerErrors[computerId].push(action.payload.error);
        } else {
          state.computerErrors[computerId] = [action.payload.error];
        }
        
        if (state.selectedComputer && state.selectedComputer.id === computerId) {
          state.selectedComputer.have_active_errors = true;
        }
        
        const computerIndex = state.computers.findIndex(c => c.id === computerId);
        if (computerIndex !== -1) {
          state.computers[computerIndex].have_active_errors = true;
        }
      })
      .addCase(reportComputerError.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      .addCase(resolveComputerError.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(resolveComputerError.fulfilled, (state, action) => {
        state.loading = false;
        const computerId = action.payload.computerId;
        const errorId = action.payload.error.id;
        
        if (state.computerErrors[computerId]) {
          const errorIndex = state.computerErrors[computerId].findIndex(e => e.id === errorId);
          if (errorIndex !== -1) {
            state.computerErrors[computerId][errorIndex] = action.payload.error;
          }
        }
        
        const allResolved = state.computerErrors[computerId]?.every(error => error.resolved) ?? true;
        
        if (state.selectedComputer && state.selectedComputer.id === computerId) {
          state.selectedComputer.have_active_errors = !allResolved;
        }
        
        const computerIndex = state.computers.findIndex(c => c.id === computerId);
        if (computerIndex !== -1) {
          state.computers[computerIndex].have_active_errors = !allResolved;
        }
      })
      .addCase(resolveComputerError.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

/**
 * Selectors for computer state
 */
export const selectComputers = (state) => state.computers.computers;
export const selectSelectedComputer = (state) => state.computers.selectedComputer;
export const selectComputerErrors = (computerId) => (state) => state.computers.computerErrors[computerId] || [];
export const selectComputerLoading = (state) => state.computers.loading;
export const selectComputerError = (state) => state.computers.error;
export const selectComputerPagination = (state) => state.computers.pagination;

export const { clearComputerError, setCurrentPage } = computerSlice.actions;
export default computerSlice.reducer; 