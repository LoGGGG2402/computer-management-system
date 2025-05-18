/**
 * Redux store configuration
 * @module store
 */

import { configureStore } from '@reduxjs/toolkit';
import authReducer from './slices/authSlice';
import socketReducer, { socketMiddleware } from './slices/socketSlice';
import commandReducer, { commandSocketMiddleware } from './slices/commandSlice';
import computerReducer from './slices/computerSlice';
import roomReducer from './slices/roomSlice';
import userReducer from './slices/userSlice';
import adminReducer from './slices/adminSlice';

/**
 * The Redux store instance
 * @type {import('@reduxjs/toolkit').Store}
 */
export const store = configureStore({
  reducer: {
    auth: authReducer,
    socket: socketReducer,
    command: commandReducer,
    computers: computerReducer,
    rooms: roomReducer,
    users: userReducer,
    admin: adminReducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: false,
    }).concat(
      socketMiddleware,
      commandSocketMiddleware
    ),
}); 