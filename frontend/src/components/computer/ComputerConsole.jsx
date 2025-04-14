import React, { useState, useCallback, useEffect, useRef } from 'react';
// Khôi phục import gốc
import { useCommandHandle } from '../../contexts/CommandHandleContext';
// Import Ant Design Spin và Icons
import { Spin } from 'antd';
import {
    CodeOutlined,
    DeleteOutlined,
    SendOutlined,
    DisconnectOutlined, // Icon cho trạng thái offline
    LoadingOutlined,  // Icon cho trạng thái loading
    CloseOutlined     // Icon cho nút đóng alert
} from '@ant-design/icons';

// --- Component ComputerConsole (Sử dụng Context thật và Ant Design Icons) ---
const ComputerConsole = ({ computerId, computer, isOnline = false }) => {
    const [commandInput, setCommandInput] = useState('');
    const [output, setOutput] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState(null);
    // Sử dụng hook thật từ context đã import
    const { sendCommand, commandResults } = useCommandHandle();
    const outputEndRef = useRef(null);
    // Counter này chủ yếu để tạo key duy nhất cho các phần tử feedback tức thời (live)
    const localIdCounter = useRef(0);

    // Hàm cuộn xuống dưới cùng của output
    const scrollToBottom = () => {
        outputEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    };

    // Lấy kết quả lệnh cho computerId cụ thể từ context
    const computerCommandResults = computerId ? commandResults[computerId] || [] : [];

    // Effect để cập nhật output hiển thị khi commandResults thay đổi
    useEffect(() => {
        // console.log(`[Console] Rendering output for ${computerId}`);
        const allEntries = [];
        computerCommandResults.forEach((result, index) => {
            const timestamp = new Date(result.timestamp);
            const sessionHasError = (result.stderr && result.stderr.trim()) || (result.exitCode !== 0);

            // Thêm dòng phân cách session
            if (index > 0) {
                allEntries.push({
                    id: `session-${result.commandId || index}-separator`, type: 'separator',
                    content: `--- Session ended: ${timestamp.toLocaleString()} ---`,
                    timestamp: timestamp, showTimestamp: false, hasError: false,
                });
            }
            // Thêm dòng lệnh input
            allEntries.push({
                id: `restored-${result.commandId}-input`, commandId: result.commandId, type: 'input',
                content: `$ ${result.commandText || '[Command text not available]'}`,
                timestamp: timestamp, showTimestamp: true, hasError: sessionHasError,
            });
            // Thêm stdout nếu có
            if (result.stdout && result.stdout.trim()) {
                allEntries.push({
                    id: `restored-${result.commandId}-stdout`, commandId: result.commandId, type: 'output',
                    content: result.stdout.trim(), timestamp: timestamp, showTimestamp: false, hasError: sessionHasError,
                });
            }
            // Thêm stderr nếu có
            if (result.stderr && result.stderr.trim()) {
                allEntries.push({
                    id: `restored-${result.commandId}-stderr`, commandId: result.commandId, type: 'error',
                    content: `stderr: ${result.stderr.trim()}`, timestamp: timestamp, showTimestamp: false, hasError: sessionHasError,
                });
            }
            // Thêm exit code
            allEntries.push({
                id: `restored-${result.commandId}-exit`, commandId: result.commandId, type: 'exit',
                content: `[Process exited with code: ${result.exitCode}]`,
                timestamp: timestamp, showTimestamp: false, hasError: sessionHasError,
            });
        });
        setOutput(allEntries);
        // Cuộn xuống dưới sau khi render kết quả
        const timer = setTimeout(scrollToBottom, 100);
        return () => clearTimeout(timer);
    }, [computerId, computerCommandResults]); // Chạy lại khi computerId hoặc kết quả của nó thay đổi

    // Xử lý gửi lệnh mới
    const handleSendCommand = useCallback(async () => {
        if (!commandInput.trim() || !computerId || !isOnline) {
             if (!isOnline) setError("Cannot execute commands: computer is offline");
             return;
        }
        const commandToSend = commandInput.trim();
        const timestamp = new Date();
        const currentLocalId = localIdCounter.current++;

        // Thêm dòng input vào output ngay lập tức để phản hồi nhanh
        setOutput(prev => [
            ...prev,
            ...(prev.length > 0 ? [{ id: `live-separator-${currentLocalId}`, type: 'separator', content: `--- Session ended: ${timestamp.toLocaleString()} ---`, timestamp: timestamp, showTimestamp: false, hasError: false }] : []),
            { id: `live-cmd-${currentLocalId}-input`, commandId: null, type: 'input', content: `$ ${commandToSend}`, timestamp: timestamp, showTimestamp: true, hasError: false }
        ]);
        setCommandInput('');
        setIsLoading(true);
        setError(null);
        scrollToBottom();

        try {
            // Gọi hàm sendCommand thật từ context
            await sendCommand(computerId, commandToSend);
        } catch (err) {
             // Xử lý lỗi nếu hàm sendCommand (hoặc quá trình chờ kết quả) bị reject
            const errorTimestamp = new Date();
            console.error('[Console] Error sending command:', err);
            const errorMessage = `Error: ${err.message || 'Failed to execute command'}`;
            // Thêm lỗi vào output hiển thị trực tiếp
            setOutput(prev => [...prev, {
                id: `live-cmd-${currentLocalId}-exec-error`, commandId: null, type: 'error',
                content: errorMessage, timestamp: errorTimestamp, showTimestamp: false, hasError: true
            }]);
            setError(errorMessage); // Hiển thị lỗi chung
            scrollToBottom();
        } finally {
            setIsLoading(false);
        }
    }, [commandInput, computerId, sendCommand, isOnline, output]);

    // Các hàm xử lý input và clear console
    const handleInputChange = (e) => setCommandInput(e.target.value);
    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && !e.shiftKey && !isLoading) {
            e.preventDefault();
            handleSendCommand();
        }
    };
    const clearConsole = useCallback(() => { setOutput([]); setError(null); }, []);

    // Hàm lấy class Tailwind cho style chữ dựa trên loại dòng
    const getContentTextClasses = (type) => {
        switch (type) {
            case 'input': return 'text-blue-700 font-bold';
            case 'error': return 'text-red-700';
            case 'exit': return 'text-gray-600 italic'; // Bỏ mt-1 vì padding đã xử lý khoảng cách
            case 'output': default: return 'text-gray-800';
        }
    };

    // --- Phần JSX Render ---
    return (
        // Container chính (thay thế Card)
        <div className="bg-white shadow-md rounded-lg border border-gray-200 flex flex-col h-full min-h-[400px] max-h-[75vh]">
            {/* Header */}
            <div className="p-3 border-b border-gray-200 flex items-center justify-between flex-shrink-0">
                <div className="flex items-center gap-2">
                    {/* Sử dụng Ant Design Icon */}
                    <CodeOutlined />
                    <span className="font-semibold text-gray-700">Console - {computer?.name || computerId}</span>
                </div>
                <button
                    onClick={clearConsole}
                    title="Clear Console Display" // Tooltip dùng title attribute
                    className="p-1 rounded text-gray-500 hover:bg-red-100 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-300"
                >
                    {/* Sử dụng Ant Design Icon */}
                    <DeleteOutlined />
                </button>
            </div>

            {/* Body */}
            <div className="flex-grow overflow-hidden flex flex-col p-2">
                 {/* Cảnh báo Offline */}
                {!isOnline && (
                    <div className="mb-2 p-3 border border-yellow-300 bg-yellow-50 rounded-md flex items-center gap-3 flex-shrink-0">
                         {/* Sử dụng Ant Design Icon */}
                         <DisconnectOutlined className="text-yellow-600 text-lg" />
                         <div>
                            <p className="font-semibold text-yellow-800">Computer Offline</p>
                            <p className="text-sm text-yellow-700">This computer is currently offline. Commands cannot be executed.</p>
                         </div>
                    </div>
                )}

                {/* Khu vực hiển thị Output */}
                <div className="flex-grow overflow-y-auto bg-gray-50 p-3 rounded border border-gray-200 font-mono text-sm leading-relaxed mb-2">
                    {/* Thông báo khi chưa có output */}
                    {output.length === 0 && !isLoading && (
                         <div className="text-center text-gray-500 mt-4">
                            {computerCommandResults.length > 0 ? 'Command history loaded.' : 'Enter a command to start.'}
                        </div>
                    )}
                    {/* Map qua các dòng output */}
                    {output.map((line) => {
                        // Render dòng phân cách
                        if (line.type === 'separator') {
                            return (
                                <div key={line.id} className="text-center text-xs text-gray-500 italic border-t border-dashed border-gray-300 my-4 pt-1">
                                    {line.content}
                                </div>
                            );
                        }
                        // Render các dòng lệnh (input, output, error, exit)
                        return (
                            <div
                                key={line.id}
                                // Style cơ bản + nền lỗi có điều kiện (đã bỏ mb-0.5)
                                className={`flex px-1 py-0.5 rounded ${line.hasError ? 'bg-red-50' : ''}`}
                            >
                                {/* Khu vực timestamp */}
                                <div className={`flex-shrink-0 w-[90px] mr-2 text-gray-500 ${line.showTimestamp ? 'visible' : 'invisible'}`}>
                                   {line.showTimestamp ? `[${line.timestamp.toLocaleTimeString()}]` : ''}
                                </div>
                                {/* Khu vực nội dung chính */}
                                <div className={`flex-grow whitespace-pre-wrap break-all ${getContentTextClasses(line.type)}`}>
                                    {line.content}
                                </div>
                            </div>
                        );
                    })}
                    {/* Chỉ báo Loading */}
                    {isLoading && (
                        <div className="flex justify-center items-center gap-2 text-gray-600 py-2">
                             {/* Sử dụng Ant Design Spin và Icon */}
                            <Spin indicator={<LoadingOutlined style={{ fontSize: 16 }} spin />} size="small" />
                            <span className="ml-1">Running command...</span>
                        </div>
                    )}
                    {/* Điểm neo để cuộn */}
                    <div ref={outputEndRef} />
                </div>

                 {/* Cảnh báo lỗi chung */}
                {error && (
                     <div className="mb-2 p-3 border border-red-300 bg-red-50 text-red-700 rounded-md flex justify-between items-center flex-shrink-0">
                        <span>Operation Error: {error}</span>
                        <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">
                             {/* Sử dụng Ant Design Icon */}
                            <CloseOutlined />
                        </button>
                    </div>
                )}

                {/* Khu vực Input lệnh */}
                <div className="flex gap-2 flex-shrink-0">
                    <input
                        type="text"
                        placeholder={isOnline ? "Enter command..." : "Computer offline - commands disabled"}
                        value={commandInput}
                        onChange={handleInputChange}
                        onKeyPress={handleKeyPress}
                        disabled={isLoading || !isOnline}
                        className="flex-grow px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm disabled:bg-gray-100 font-mono"
                    />
                    <button
                        type="button"
                        onClick={handleSendCommand}
                        disabled={isLoading || !commandInput.trim() || !isOnline}
                        title={isOnline ? "Send Command (Enter)" : "Computer is offline"} // Tooltip dùng title attribute
                        className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {/* Hiển thị spinner hoặc icon gửi */}
                        {isLoading
                            ? <Spin indicator={<LoadingOutlined style={{ fontSize: 18, color: 'white' }} spin />} size="small" />
                            : <SendOutlined />
                        }
                    </button>
                </div>
            </div>
        </div>
    );
};

// Export component ComputerConsole làm mặc định
export default ComputerConsole;
