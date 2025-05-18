/**
 * Room management slice for Redux store
 * @module roomSlice
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import roomService from '../services/room.service';

/**
 * Async thunk to fetch all rooms with optional filters
 * @param {Object} filters - Optional filters for room query
 * @returns {Promise} Promise resolving to room data
 */
export const fetchRooms = createAsyncThunk(
  'rooms/fetchAll',
  async (filters = {}, { rejectWithValue }) => {
    try {
      return await roomService.getAllRooms(filters);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch a single room by ID
 * @param {string|number} id - Room ID to fetch
 * @returns {Promise} Promise resolving to room data
 */
export const fetchRoomById = createAsyncThunk(
  'rooms/fetchById',
  async (id, { rejectWithValue }) => {
    try {
      return await roomService.getRoomById(id);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to create a new room
 * @param {Object} roomData - Room data for creation
 * @returns {Promise} Promise resolving to created room
 */
export const createRoom = createAsyncThunk(
  'rooms/create',
  async (roomData, { rejectWithValue }) => {
    try {
      return await roomService.createRoom(roomData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to update an existing room
 * @param {Object} params - Update parameters
 * @param {string|number} params.id - Room ID to update
 * @param {Object} params.roomData - New room data
 * @returns {Promise} Promise resolving to updated room
 */
export const updateRoom = createAsyncThunk(
  'rooms/update',
  async ({ id, roomData }, { rejectWithValue }) => {
    try {
      return await roomService.updateRoom(id, roomData);
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to delete a room
 * @param {string|number} id - Room ID to delete
 * @returns {Promise} Promise resolving to deleted room ID
 */
export const deleteRoom = createAsyncThunk(
  'rooms/delete',
  async (id, { rejectWithValue }) => {
    try {
      await roomService.deleteRoom(id);
      return id;
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Async thunk to fetch computers in a room
 * @param {string|number} id - Room ID to fetch computers for
 * @returns {Promise} Promise resolving to room's computers
 */
export const fetchRoomComputers = createAsyncThunk(
  'rooms/fetchComputers',
  async (id, { rejectWithValue }) => {
    try {
      return {
        roomId: id,
        computers: await roomService.getRoomComputers(id)
      };
    } catch (error) {
      return rejectWithValue(error.message);
    }
  }
);

/**
 * Initial state for room slice
 * @type {Object}
 */
const initialState = {
  rooms: [],
  selectedRoom: null,
  roomComputers: {},
  loading: false,
  error: null,
  pagination: {
    total: 0,
    currentPage: 1,
    totalPages: 1
  }
};

/**
 * Room slice for Redux store
 * @type {import('@reduxjs/toolkit').Slice}
 */
const roomSlice = createSlice({
  name: 'rooms',
  initialState,
  reducers: {
    clearRoomError: (state) => {
      state.error = null;
    },
    clearSelectedRoom: (state) => {
      state.selectedRoom = null;
    },
    setCurrentPage: (state, action) => {
      state.pagination.currentPage = action.payload;
    }
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchRooms.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchRooms.fulfilled, (state, action) => {
        state.loading = false;
        state.rooms = action.payload.rooms;
        state.pagination.total = action.payload.total;
        state.pagination.currentPage = action.payload.currentPage;
        state.pagination.totalPages = action.payload.totalPages;
      })
      .addCase(fetchRooms.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(fetchRoomById.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchRoomById.fulfilled, (state, action) => {
        state.loading = false;
        state.selectedRoom = action.payload;
      })
      .addCase(fetchRoomById.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(createRoom.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(createRoom.fulfilled, (state, action) => {
        state.loading = false;
        state.rooms.push(action.payload);
      })
      .addCase(createRoom.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(updateRoom.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(updateRoom.fulfilled, (state, action) => {
        state.loading = false;
        const index = state.rooms.findIndex(room => room.id === action.payload.id);
        if (index !== -1) {
          state.rooms[index] = action.payload;
        }
        if (state.selectedRoom && state.selectedRoom.id === action.payload.id) {
          state.selectedRoom = action.payload;
        }
      })
      .addCase(updateRoom.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(deleteRoom.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(deleteRoom.fulfilled, (state, action) => {
        state.loading = false;
        state.rooms = state.rooms.filter(room => room.id !== action.payload);
        if (state.selectedRoom && state.selectedRoom.id === action.payload) {
          state.selectedRoom = null;
        }
        if (state.roomComputers[action.payload]) {
          delete state.roomComputers[action.payload];
        }
      })
      .addCase(deleteRoom.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      })
      
      .addCase(fetchRoomComputers.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchRoomComputers.fulfilled, (state, action) => {
        state.loading = false;
        state.roomComputers[action.payload.roomId] = action.payload.computers;
      })
      .addCase(fetchRoomComputers.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

/**
 * Selectors for room state
 */
export const selectRooms = (state) => state.rooms.rooms;
export const selectSelectedRoom = (state) => state.rooms.selectedRoom;
export const selectRoomComputers = (roomId) => (state) => state.rooms.roomComputers[roomId] || [];
export const selectRoomLoading = (state) => state.rooms.loading;
export const selectRoomError = (state) => state.rooms.error;
export const selectRoomPagination = (state) => state.rooms.pagination;

// Room layout related selectors
export const selectRoomLayout = (state) => state.rooms.selectedRoom?.layout || null;
export const selectRoomLayoutLoading = (state) => state.rooms.loading;
export const selectRoomComputersLoading = (state) => state.rooms.loading;

// Room layout related actions
export const fetchRoomLayout = fetchRoomById; // Reuse fetchRoomById for layout

export const { clearRoomError, clearSelectedRoom, setCurrentPage } = roomSlice.actions;
export default roomSlice.reducer; 