/**
 * Core Redux store export
 * @module index
 */
export { store } from './store';

// ==============================
// =======  Core Hooks  =========
// ==============================

/**
 * Core Redux hooks for state management
 * @module hooks
 */
export {
  // Base hooks
  useAppSelector,
  useAppDispatch,
  
  // State hooks
  useAuthState,
  useSocketState,
  useComputerState,
  useRoomState,
  useUserState,
  useCommandState,
  useAdminState
} from './hooks/useReduxSelector';

/**
 * UI and utility hooks for common functionality
 * @module uiHooks
 */
export { 
  useCopyToClipboard,
  useFormatting,
  useModalState
} from './hooks/useUITools';

/**
 * Data fetching hook for Redux operations
 * @module dataHooks
 */
export { useReduxFetch } from './hooks/useDataFetch';

// ==============================
// =======  Slice Exports  ======
// ==============================

/**
 * Authentication slice exports
 * @module auth
 */
export {
  // Actions
  initializeAuth,
  login,
  logout,
  refreshToken,
  clearError as clearAuthError,
  
  // Selectors
  selectAuthUser,
  selectAuthLoading,
  selectAuthError,
  selectIsAuthenticated,
  selectUserRole
} from './slices/authSlice';

/**
 * Socket management slice exports
 * @module socket
 */
export {
  // Actions
  initializeSocket,
  disconnectSocket,
  clearSocketError,
  receiveNewAgentMFA,
  receiveAgentRegistered,
  clearPendingAgentMFA,
  
  // Selectors
  selectSocketInstance,
  selectSocketConnected,
  selectSocketLoading,
  selectSocketError,
  selectSocketEvents,
  selectOnlineComputers,
  selectOfflineComputers,
  selectComputerStatuses,
  selectComputerStatus,
  selectSocketComputerErrors,
  selectPendingAgentMFA,
  selectRegisteredAgents
} from './slices/socketSlice';

/**
 * Computer management slice exports
 * @module computer
 */
export {
  // Actions
  fetchComputers,
  fetchComputerById,
  deleteComputer,
  fetchComputerErrors,
  reportComputerError,
  resolveComputerError,
  clearComputerError,
  setCurrentPage as setComputerCurrentPage,
  
  // Selectors
  selectComputers,
  selectSelectedComputer,
  selectComputerErrors,
  selectComputerLoading,
  selectComputerError,
  selectComputerPagination
} from './slices/computerSlice';

/**
 * Room management slice exports
 * @module room
 */
export {
  // Actions
  fetchRooms,
  fetchRoomById,
  createRoom,
  updateRoom,
  deleteRoom,
  fetchRoomComputers,
  clearRoomError,
  clearSelectedRoom,
  setCurrentPage as setRoomCurrentPage,
  fetchRoomLayout,
  
  // Selectors
  selectRooms,
  selectSelectedRoom,
  selectRoomComputers,
  selectRoomLoading,
  selectRoomError,
  selectRoomPagination,
  selectRoomLayout,
  selectRoomLayoutLoading,
  selectRoomComputersLoading
} from './slices/roomSlice';

/**
 * User management slice exports
 * @module user
 */
export {
  // Actions
  fetchUsers,
  fetchUserById,
  createUser,
  updateUser,
  deleteUser,
  fetchUserRooms,
  updateUserRooms,
  resetUserPassword,
  clearUserError,
  clearSelectedUser,
  setCurrentPage as setUserCurrentPage,
  
  // Selectors
  selectUsers,
  selectSelectedUser,
  selectUserRooms,
  selectUserLoading,
  selectUserError,
  selectUserPagination
} from './slices/userSlice';

/**
 * Command management slice exports
 * @module command
 */
export {
  // Actions
  sendCommand,
  clearCommandError,
  addPendingCommand,
  updateCommandStatus,
  receiveCommandResult,
  clearCommandHistory,
  
  // Selectors
  selectCommandLoading,
  selectCommandError,
  selectAvailableCommands,
  selectPendingCommands,
  selectCommandHistory,
  selectCommandResult
} from './slices/commandSlice';

/**
 * Admin management slice exports
 * @module admin
 */
export {
  // Actions
  fetchSystemStats,
  fetchAgentVersions,
  uploadAgentVersion,
  updateAgentVersionStability,
  clearAdminError,
  
  // Selectors
  selectSystemStats,
  selectAgentVersions,
  selectAdminLoading,
  selectAdminError
} from './slices/adminSlice'; 