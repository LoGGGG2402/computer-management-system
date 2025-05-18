# 🚀 Redux Toolkit - Hệ thống Quản lý Trạng thái

## 📑 Mục lục
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [Tổng quan](#-tổng-quan)
- [Core Hooks](#-core-hooks)
  - [Redux Selector Hooks](#redux-selector-hooks)
  - [UI & Utility Hooks](#ui--utility-hooks)
  - [Data Fetching Hook](#data-fetching-hook)
- [Redux Slices](#-redux-slices)
  - [Auth Slice](#auth-slice)
  - [Socket Slice](#socket-slice)
  - [Computer Slice](#computer-slice)
  - [Room Slice](#room-slice)
  - [User Slice](#user-slice)
  - [Command Slice](#command-slice)
  - [Admin Slice](#admin-slice)

## 📁 Cấu trúc thư mục

```
/src/app/
├── index.js                  # File export chính
├── store.js                  # Redux store chính
├── hooks/                    # Custom hooks để tương tác với Redux
│   ├── useReduxSelector.js   # Hooks truy cập state Redux
│   ├── useUITools.js         # UI & Tiện ích
│   └── useDataFetch.js       # Hooks fetch dữ liệu
└── slices/                   # Redux slices
    ├── authSlice.js          # Quản lý xác thực và token
    ├── socketSlice.js        # Quản lý kết nối socket và trạng thái
    ├── commandSlice.js       # Quản lý lệnh máy tính
    ├── computerSlice.js      # Quản lý danh sách và thông tin máy tính
    ├── roomSlice.js          # Quản lý danh sách và thông tin phòng
    ├── userSlice.js          # Quản lý danh sách và thông tin người dùng
    └── adminSlice.js         # Quản lý chức năng quản trị viên
```

## 🎯 Tổng quan

Hệ thống quản lý trạng thái được xây dựng trên Redux Toolkit, cung cấp các tính năng:
- Quản lý xác thực và phân quyền
- Kết nối realtime qua Socket.IO
- Quản lý máy tính và phòng học
- Xử lý lệnh và giám sát hệ thống
- Quản trị hệ thống

## 🔧 Core Hooks

### Redux Selector Hooks
| Hook | Mô tả |
|------|--------|
| `useAppSelector()` | Hook tiện ích để memoize selector |
| `useAppDispatch()` | Hook tiện ích tạo dispatcher đã memoize |
| `useAuthState()` | Quản lý trạng thái xác thực |
| `useSocketState()` | Quản lý trạng thái socket |
| `useComputerState()` | Quản lý trạng thái máy tính |
| `useRoomState()` | Quản lý trạng thái phòng |
| `useUserState()` | Quản lý trạng thái người dùng |
| `useCommandState()` | Quản lý trạng thái lệnh |
| `useAdminState()` | Quản lý trạng thái admin |

### UI & Utility Hooks
| Hook | Mô tả |
|------|--------|
| `useCopyToClipboard()` | Sao chép văn bản vào clipboard |
| `useFormatting()` | Các hàm định dạng dữ liệu |
| `useModalState()` | Quản lý trạng thái modal |

### Data Fetching Hook
| Hook | Mô tả |
|------|--------|
| `useReduxFetch()` | Hook tối ưu để fetch dữ liệu |

## 📦 Redux Slices

### Auth Slice
Quản lý xác thực và phân quyền người dùng.

#### Actions
| Action | Mô tả |
|--------|--------|
| `initializeAuth()` | Khởi tạo trạng thái xác thực |
| `login({ username, password })` | Đăng nhập |
| `logout()` | Đăng xuất |
| `refreshToken()` | Làm mới token |
| `clearError()` | Xóa lỗi |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectAuthUser` | Lấy thông tin người dùng |
| `selectAuthLoading` | Trạng thái loading |
| `selectAuthError` | Lỗi xác thực |
| `selectIsAuthenticated` | Kiểm tra đã xác thực |
| `selectUserRole` | Vai trò người dùng |

### Socket Slice
Quản lý kết nối realtime và trạng thái máy tính.

#### Actions
| Action | Mô tả |
|--------|--------|
| `initializeSocket()` | Khởi tạo kết nối socket |
| `disconnectSocket()` | Ngắt kết nối socket |
| `clearSocketError()` | Xóa lỗi socket |
| `receiveNewAgentMFA()` | Nhận MFA từ agent mới |
| `receiveAgentRegistered()` | Nhận thông báo agent đã đăng ký |
| `clearPendingAgentMFA()` | Xóa MFA đang chờ |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectSocketInstance` | Instance socket |
| `selectSocketConnected` | Trạng thái kết nối |
| `selectSocketLoading` | Trạng thái loading |
| `selectSocketError` | Lỗi socket |
| `selectSocketEvents` | Các sự kiện socket |
| `selectOnlineComputers` | Máy tính đang online |
| `selectOfflineComputers` | Máy tính đang offline |
| `selectComputerStatuses` | Trạng thái các máy tính |
| `selectComputerStatus` | Trạng thái một máy tính |
| `selectSocketComputerErrors` | Lỗi máy tính từ socket |
| `selectPendingAgentMFA` | MFA đang chờ |
| `selectRegisteredAgents` | Agent đã đăng ký |

### Computer Slice
Quản lý thông tin và trạng thái máy tính.

#### Actions
| Action | Mô tả |
|--------|--------|
| `fetchComputers()` | Lấy danh sách máy tính |
| `fetchComputerById()` | Lấy thông tin máy tính theo ID |
| `deleteComputer()` | Xóa máy tính |
| `fetchComputerErrors()` | Lấy lỗi máy tính |
| `reportComputerError()` | Báo cáo lỗi máy tính |
| `resolveComputerError()` | Giải quyết lỗi máy tính |
| `clearComputerError()` | Xóa lỗi máy tính |
| `setCurrentPage()` | Đặt trang hiện tại |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectComputers` | Danh sách máy tính |
| `selectSelectedComputer` | Máy tính được chọn |
| `selectComputerErrors` | Lỗi máy tính |
| `selectComputerLoading` | Trạng thái loading |
| `selectComputerError` | Lỗi |
| `selectComputerPagination` | Phân trang |

### Room Slice
Quản lý thông tin phòng học và máy tính trong phòng.

#### Actions
| Action | Mô tả |
|--------|--------|
| `fetchRooms()` | Lấy danh sách phòng |
| `fetchRoomById()` | Lấy thông tin phòng theo ID |
| `createRoom()` | Tạo phòng mới |
| `updateRoom()` | Cập nhật phòng |
| `deleteRoom()` | Xóa phòng |
| `fetchRoomComputers()` | Lấy máy tính trong phòng |
| `clearRoomError()` | Xóa lỗi phòng |
| `clearSelectedRoom()` | Xóa phòng được chọn |
| `setCurrentPage()` | Đặt trang hiện tại |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectRooms` | Danh sách phòng |
| `selectSelectedRoom` | Phòng được chọn |
| `selectRoomComputers` | Máy tính trong phòng |
| `selectRoomLoading` | Trạng thái loading |
| `selectRoomError` | Lỗi |
| `selectRoomPagination` | Phân trang |

### User Slice
Quản lý thông tin và phân quyền người dùng.

#### Actions
| Action | Mô tả |
|--------|--------|
| `fetchUsers()` | Lấy danh sách người dùng |
| `fetchUserById()` | Lấy thông tin người dùng theo ID |
| `createUser()` | Tạo người dùng mới |
| `updateUser()` | Cập nhật người dùng |
| `deleteUser()` | Xóa người dùng |
| `fetchUserRooms()` | Lấy phòng của người dùng |
| `updateUserRooms()` | Cập nhật phòng người dùng |
| `resetUserPassword()` | Đặt lại mật khẩu |
| `clearUserError()` | Xóa lỗi người dùng |
| `clearSelectedUser()` | Xóa người dùng được chọn |
| `setCurrentPage()` | Đặt trang hiện tại |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectUsers` | Danh sách người dùng |
| `selectSelectedUser` | Người dùng được chọn |
| `selectUserRooms` | Phòng của người dùng |
| `selectUserLoading` | Trạng thái loading |
| `selectUserError` | Lỗi |
| `selectUserPagination` | Phân trang |

### Command Slice
Quản lý lệnh và kết quả thực thi.

#### Actions
| Action | Mô tả |
|--------|--------|
| `sendCommand()` | Gửi lệnh |
| `clearCommandError()` | Xóa lỗi lệnh |
| `addPendingCommand()` | Thêm lệnh đang chờ |
| `updateCommandStatus()` | Cập nhật trạng thái lệnh |
| `receiveCommandResult()` | Nhận kết quả lệnh |
| `clearCommandHistory()` | Xóa lịch sử lệnh |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectCommandLoading` | Trạng thái loading |
| `selectCommandError` | Lỗi |
| `selectAvailableCommands` | Lệnh có sẵn |
| `selectPendingCommands` | Lệnh đang chờ |
| `selectCommandHistory` | Lịch sử lệnh |
| `selectCommandResult` | Kết quả lệnh |

### Admin Slice
Quản lý chức năng quản trị hệ thống.

#### Actions
| Action | Mô tả |
|--------|--------|
| `fetchSystemStats()` | Lấy thống kê hệ thống |
| `fetchAgentVersions()` | Lấy phiên bản agent |
| `uploadAgentVersion()` | Tải lên phiên bản agent |
| `updateAgentVersionStability()` | Cập nhật độ ổn định phiên bản |
| `clearAdminError()` | Xóa lỗi admin |

#### Selectors
| Selector | Mô tả |
|----------|--------|
| `selectSystemStats` | Thống kê hệ thống |
| `selectAgentVersions` | Phiên bản agent |
| `selectAdminLoading` | Trạng thái loading |
| `selectAdminError` | Lỗi | 