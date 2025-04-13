import React, { createContext, useContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useSocket } from './SocketContext';

// Tạo context
const CommandHandleContext = createContext(null);

/**
 * Provider component để quản lý việc thực thi lệnh và kết quả trên toàn ứng dụng
 * Xử lý gửi lệnh và theo dõi kết quả lệnh mới nhất cho mỗi máy tính
 */
export const CommandHandleProvider = ({ children }) => {
  // Lưu trữ kết quả theo computerId: { computerId: { stdout, stderr, exitCode, timestamp, commandId } }
  const [commandResults, setCommandResults] = useState({});
  // Theo dõi các promise của lệnh để giải quyết chúng khi có phản hồi
  // Sử dụng Ref để tránh việc thay đổi state này gây render lại không cần thiết
  const commandPromisesRef = useRef({});
  // Theo dõi trạng thái gửi lệnh (đã gửi thành công hay thất bại từ server)
  const [commandStatus, setCommandStatus] = useState({});

  // Lấy socket từ SocketContext
  const { socket } = useSocket(); // Chỉ cần socket instance

  // Lắng nghe các sự kiện hoàn thành lệnh và trạng thái gửi lệnh từ socket
  useEffect(() => {
    // Chỉ thực hiện nếu socket tồn tại
    if (!socket) return;

    // Xử lý khi lệnh hoàn thành trên agent
    const handleCommandCompleted = (data) => {
      console.log('[CommandHandleContext] Nhận kết quả lệnh:', data);
      if (data && data.computerId && data.commandId) {
          // Lưu trữ kết quả theo computerId
          setCommandResults(prevResults => ({
            ...prevResults,
            [data.computerId]: {
              stdout: data.stdout,
              stderr: data.stderr,
              exitCode: data.exitCode,
              timestamp: Date.now(),
              commandId: data.commandId // Lưu cả commandId để tham chiếu
            }
          }));
      } else {
           console.warn("Nhận dữ liệu command:completed không hợp lệ:", data);
      }
    };

    // Xử lý phản hồi xác nhận lệnh đã được gửi tới agent (hoặc lỗi)
    const handleCommandSentStatus = (data) => {
      console.log('Trạng thái gửi lệnh:', data);
       if (data && data.commandId) {
           // Cập nhật trạng thái gửi lệnh
           setCommandStatus(prev => ({
             ...prev,
             [data.commandId]: {
               status: data.status, // 'success' hoặc 'error'
               message: data.message, // Thông báo lỗi nếu có
               timestamp: Date.now()
             }
           }));

           // Tìm và giải quyết/từ chối promise tương ứng
           const promiseCallbacks = commandPromisesRef.current[data.commandId];
           if (promiseCallbacks) {
             if (data.status === 'success') {
               promiseCallbacks.resolve(data); // Giải quyết với thông tin trạng thái
             } else {
               promiseCallbacks.reject(new Error(data.message || 'Không thể gửi lệnh đến agent')); // Từ chối với lỗi
             }
             // Xóa promise đã xử lý khỏi ref
             delete commandPromisesRef.current[data.commandId];
           }
       } else {
            console.warn("Nhận dữ liệu command_sent không hợp lệ:", data);
       }
    };

    // Đăng ký listeners
    socket.on('command:completed', handleCommandCompleted);
    socket.on('command_sent', handleCommandSentStatus);

    // Cleanup: Hủy đăng ký listeners khi component unmount hoặc socket thay đổi
    return () => {
      if (socket) {
        socket.off('command:completed', handleCommandCompleted);
        socket.off('command_sent', handleCommandSentStatus);
      }
      // Không cần xóa promises ở đây vì chúng sẽ tự bị xóa khi được giải quyết/từ chối hoặc timeout
    };
  }, [socket]); // Phụ thuộc vào socket instance

  // Chức năng cốt lõi - gửi lệnh đến một agent - sử dụng useCallback
  const sendCommand = useCallback((computerId, command) => {
     // Kiểm tra socket tồn tại và đã kết nối
    if (!socket || !socket.connected) {
      console.error('Không thể gửi lệnh: Socket chưa kết nối.');
      return Promise.reject(new Error('Chưa kết nối với máy chủ qua socket'));
    }

    if (!computerId || !command) {
        console.error('Không thể gửi lệnh: computerId hoặc command không hợp lệ.');
        return Promise.reject(new Error('Computer ID và command là bắt buộc'));
    }

    console.log(`Gửi lệnh đến computer ${computerId}: ${command}`);

    // Tạo một promise mới cho lệnh này
    return new Promise((resolve, reject) => {
      // Backend sẽ tạo commandId và gửi lại trong sự kiện 'command_sent'
      const payload = { computerId, command };
      const tempId = `temp_${Date.now()}_${Math.random().toString(36).substring(7)}`; // ID tạm thời duy nhất

      // Lưu trữ callbacks của promise vào ref, sử dụng tempId làm key tạm thời
      // Backend sẽ trả về commandId thật sự trong event 'command_sent'
      // Chúng ta cần một cách để liên kết tempId với commandId thật sự sau này
      // -> Cách đơn giản hơn: Backend trả về commandId ngay khi emit 'command_sent'
      // -> Chúng ta sẽ dùng commandId đó để lưu và giải quyết promise.

      // Gửi lệnh qua socket
      socket.emit('frontend:send_command', payload, (ack) => {
          // Callback này (acknowledgement) từ emit thường được dùng để xác nhận server đã nhận
          // nhưng không đảm bảo lệnh đã được gửi tới agent.
          // Chúng ta sẽ dựa vào sự kiện 'command_sent' để biết trạng thái thực sự.
          if (ack && ack.commandId) {
              const commandId = ack.commandId;
              console.log(`Lệnh đã được server nhận, commandId: ${commandId}`);

              // Lưu promise callbacks với commandId thật sự
              commandPromisesRef.current[commandId] = { resolve, reject };

              // Đặt timeout để từ chối promise nếu không nhận được phản hồi 'command_sent'
              const timeoutId = setTimeout(() => {
                if (commandPromisesRef.current[commandId]) {
                  console.warn(`Command ${commandId} timed out chờ phản hồi 'command_sent'.`);
                  commandPromisesRef.current[commandId].reject(new Error('Lệnh gửi đi bị timeout (không có phản hồi từ server)'));
                  delete commandPromisesRef.current[commandId]; // Xóa promise khỏi ref
                }
              }, 15000); // Timeout 15 giây chờ server xác nhận gửi

              // Lưu timeoutId để có thể xóa nếu nhận được phản hồi sớm
               commandPromisesRef.current[commandId].timeoutId = timeoutId;

          } else {
              // Nếu server không trả về commandId trong acknowledgement
              console.error('Server không trả về commandId trong acknowledgement.');
              reject(new Error('Lỗi giao tiếp với server khi gửi lệnh.'));
          }
      });


      // Xóa timeout khi promise được giải quyết hoặc từ chối trong handleCommandSentStatus
       const originalResolve = resolve;
       const originalReject = reject;
       resolve = (value) => {
           const promiseData = commandPromisesRef.current[ack?.commandId];
           if (promiseData?.timeoutId) clearTimeout(promiseData.timeoutId);
           delete commandPromisesRef.current[ack?.commandId];
           originalResolve(value);
       };
       reject = (reason) => {
           const promiseData = commandPromisesRef.current[ack?.commandId];
           if (promiseData?.timeoutId) clearTimeout(promiseData.timeoutId);
           delete commandPromisesRef.current[ack?.commandId];
           originalReject(reason);
       };
        // Cập nhật lại promise callbacks trong ref với phiên bản đã thêm clearTimeout
       if (ack?.commandId) {
           commandPromisesRef.current[ack.commandId] = { resolve, reject, timeoutId: commandPromisesRef.current[ack.commandId]?.timeoutId };
       }


    });
  }, [socket]); // Phụ thuộc vào socket instance

  // Xóa kết quả cho một máy tính cụ thể - sử dụng useCallback
  const clearResult = useCallback((computerId) => {
    setCommandResults(prevResults => {
      // Chỉ tạo object mới nếu key tồn tại
      if (prevResults[computerId]) {
          const newResults = { ...prevResults };
          delete newResults[computerId];
          return newResults;
      }
      return prevResults; // Không thay đổi nếu key không tồn tại
    });
    // Cũng nên xóa trạng thái gửi lệnh liên quan nếu cần
    // setCommandStatus(...)
  }, []); // Không có dependencies

  // Xóa tất cả kết quả - sử dụng useCallback
  const clearAllResults = useCallback(() => {
    setCommandResults({});
    setCommandStatus({}); // Xóa cả trạng thái gửi lệnh
  }, []); // Không có dependencies

  // Tự động hết hạn kết quả sau một khoảng thời gian (ví dụ: 30 phút)
  useEffect(() => {
    const EXPIRY_TIME = 30 * 60 * 1000; // 30 phút tính bằng mili giây

    const interval = setInterval(() => {
      const now = Date.now();
      let changed = false;

      // Lọc ra các kết quả chưa hết hạn
      const newResults = Object.entries(commandResults).reduce((acc, [computerId, result]) => {
        if (now - result.timestamp <= EXPIRY_TIME) {
          acc[computerId] = result; // Giữ lại kết quả chưa hết hạn
        } else {
          changed = true; // Đánh dấu có thay đổi
          console.log(`Kết quả lệnh cho computer ${computerId} đã hết hạn.`);
        }
        return acc;
      }, {});

       // Lọc tương tự cho commandStatus
      const newStatus = Object.entries(commandStatus).reduce((acc, [commandId, status]) => {
           if(now - status.timestamp <= EXPIRY_TIME) {
               acc[commandId] = status;
           } else {
               changed = true;
           }
           return acc;
       }, {});


      // Chỉ cập nhật state nếu có thay đổi
      if (changed) {
        setCommandResults(newResults);
        setCommandStatus(newStatus);
      }
    }, 5 * 60 * 1000); // Kiểm tra mỗi 5 phút

    // Cleanup interval khi unmount
    return () => clearInterval(interval);
  }, [commandResults, commandStatus]); // Chạy lại nếu commandResults hoặc commandStatus thay đổi từ bên ngoài (ít khả năng)

  // Giá trị context được memoized - sử dụng useMemo
  const contextValue = useMemo(() => ({
    commandResults, // Kết quả lệnh { computerId: { stdout, stderr, ... } }
    commandStatus,  // Trạng thái gửi lệnh { commandId: { status, message, ... } }
    clearResult,
    clearAllResults,
    sendCommand
  }), [commandResults, commandStatus, clearResult, clearAllResults, sendCommand]); // Dependencies đầy đủ

  return (
    <CommandHandleContext.Provider value={contextValue}>
      {children}
    </CommandHandleContext.Provider>
  );
};

// Custom hook để sử dụng context
export const useCommandHandle = () => {
  const context = useContext(CommandHandleContext);
  if (!context) {
    throw new Error('useCommandHandle phải được sử dụng bên trong CommandHandleProvider');
  }
  return context;
};

// export default CommandHandleContext;
