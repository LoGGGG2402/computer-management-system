/**
 * User management slice for Redux store
 * @module userSlice
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import userService from '../services/user.service';

/**
 * Async thunk to fetch all users with optional filters
 * @param {Object} filters - Optional filters for user query
 * @returns {Promise} Promise resolving to user data
 */
export const fetchUsers = createAsyncThunk(
  'users/fetchAll',
  async (filters = {}, { rejectWithValue }) => {
    try {
      return await userService.getAllUsers(filters);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch a single user by ID
 * @param {string|number} id - User ID to fetch
 * @returns {Promise} Promise resolving to user data
 */
export const fetchUserById = createAsyncThunk(
  'users/fetchById',
  async (id, { rejectWithValue }) => {
    try {
      return await userService.getUserById(id);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to create a new user
 * @param {Object} userData - User data for creation
 * @returns {Promise} Promise resolving to created user
 */
export const createUser = createAsyncThunk(
  'users/create',
  async (userData, { rejectWithValue }) => {
    try {
      return await userService.createUser(userData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to update an existing user
 * @param {Object} params - Update parameters
 * @param {string|number} params.id - User ID to update
 * @param {Object} params.userData - New user data
 * @returns {Promise} Promise resolving to updated user
 */
export const updateUser = createAsyncThunk(
  'users/update',
  async ({ id, userData }, { rejectWithValue }) => {
    try {
      return await userService.updateUser(id, userData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to delete a user
 * @param {string|number} id - User ID to delete
 * @returns {Promise} Promise resolving to deleted user ID
 */
export const deleteUser = createAsyncThunk(
  'users/delete',
  async (id, { rejectWithValue }) => {
    try {
      await userService.deleteUser(id);
      return id;
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch rooms associated with a user
 * @param {string|number} id - User ID to fetch rooms for
 * @returns {Promise} Promise resolving to user's rooms
 */
export const fetchUserRooms = createAsyncThunk(
  'users/fetchRooms',
  async (id, { rejectWithValue }) => {
    try {
      return {
        userId: id,
        rooms: await userService.getUserRooms(id)
      };
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to update rooms associated with a user
 * @param {Object} params - Update parameters
 * @param {string|number} params.id - User ID
 * @param {Array} params.roomIds - Array of room IDs to associate
 * @returns {Promise} Promise resolving to updated user rooms
 */
export const updateUserRooms = createAsyncThunk(
  'users/updateRooms',
  async ({ id, roomIds }, { rejectWithValue }) => {
    try {
      return await userService.updateUserRooms(id, roomIds);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to reset a user's password
 * @param {Object} params - Reset parameters
 * @param {string|number} params.id - User ID
 * @param {string} params.newPassword - New password
 * @returns {Promise} Promise resolving to reset confirmation
 */
export const resetUserPassword = createAsyncThunk(
  'users/resetPassword',
  async ({ id, newPassword }, { rejectWithValue }) => {
    try {
      return await userService.resetPassword(id, newPassword);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Initial state for user slice
 * @type {Object}
 */
const initialState = {
  users: [],
  selectedUser: null,
  userRooms: {},
  loading: false,
  error: null,
  pagination: {
    total: 0,
    currentPage: 1,
    totalPages: 1
  }
};

/**
 * User slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const userSlice = createSlice({
  name: 'users',
  initialState,
  reducers: {
    clearUserError: (state) => {
      state.error = null;
    },
    clearSelectedUser: (state) => {
      state.selectedUser = null;
    },
    setCurrentPage: (state, action) => {
      state.pagination.currentPage = action.payload;
    }
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchUsers.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchUsers.fulfilled, (state, action) => {
        state.loading = false;
        state.users = action.payload.users;
        state.pagination.total = action.payload.total;
        state.pagination.currentPage = action.payload.currentPage;
        state.pagination.totalPages = action.payload.totalPages;
      })
      .addCase(fetchUsers.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(fetchUserById.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchUserById.fulfilled, (state, action) => {
        state.loading = false;
        state.selectedUser = action.payload;
      })
      .addCase(fetchUserById.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(createUser.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(createUser.fulfilled, (state, action) => {
        state.loading = false;
        state.users.push(action.payload);
      })
      .addCase(createUser.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(updateUser.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(updateUser.fulfilled, (state, action) => {
        state.loading = false;
        const index = state.users.findIndex(user => user.id === action.payload.id);
        if (index !== -1) {
          state.users[index] = action.payload;
        }
        if (state.selectedUser && state.selectedUser.id === action.payload.id) {
          state.selectedUser = action.payload;
        }
      })
      .addCase(updateUser.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(deleteUser.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(deleteUser.fulfilled, (state, action) => {
        state.loading = false;
        state.users = state.users.filter(user => user.id !== action.payload);
        if (state.selectedUser && state.selectedUser.id === action.payload) {
          state.selectedUser = null;
        }
        if (state.userRooms[action.payload]) {
          delete state.userRooms[action.payload];
        }
      })
      .addCase(deleteUser.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(fetchUserRooms.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchUserRooms.fulfilled, (state, action) => {
        state.loading = false;
        state.userRooms[action.payload.userId] = action.payload.rooms;
      })
      .addCase(fetchUserRooms.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(updateUserRooms.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(updateUserRooms.fulfilled, (state, action) => {
        state.loading = false;
        state.userRooms[action.payload.userId] = action.payload.rooms;
        if (state.selectedUser && state.selectedUser.id === action.payload.userId) {
          state.selectedUser = {
            ...state.selectedUser,
            rooms: action.payload.rooms
          };
        }
      })
      .addCase(updateUserRooms.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(resetUserPassword.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(resetUserPassword.fulfilled, (state) => {
        state.loading = false;
      })
      .addCase(resetUserPassword.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

/**
 * Selectors for user state
 */
export const selectUsers = (state) => state.users.users;
export const selectSelectedUser = (state) => state.users.selectedUser;
export const selectUserRooms = (userId) => (state) => state.users.userRooms[userId] || [];
export const selectUserLoading = (state) => state.users.loading;
export const selectUserError = (state) => state.users.error;
export const selectUserPagination = (state) => state.users.pagination;

export const { clearUserError, clearSelectedUser, setCurrentPage } = userSlice.actions;
export default userSlice.reducer; 