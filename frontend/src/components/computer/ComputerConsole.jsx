/**
 * @fileoverview Computer Console component for remote command execution
 * 
 * This component provides a terminal-like interface for executing commands
 * on remote computers. It allows users to send commands to computers,
 * displays command execution results, and maintains a history of previous
 * commands and their outputs.
 */
import React, { useState, useCallback, useEffect, useRef } from 'react';
import { Spin, Select } from 'antd';
import {
    CodeOutlined,
    DeleteOutlined,
    SendOutlined,
    DisconnectOutlined,
    LoadingOutlined,
    CloseOutlined
} from '@ant-design/icons';
import {
    useAppDispatch,
    useAppSelector,
    sendCommand,
    selectCommandHistory
} from '../../app/index';

const { Option } = Select;

/**
 * Computer Console Component
 * 
 * Provides a terminal-like interface for executing commands on remote computers.
 * Features include:
 * - Command input with type selection (console/script/action)
 * - Real-time command execution display
 * - Command history with timestamped entries
 * - Error handling and offline state management
 * - Output formatting for different content types
 * 
 * @component
 * @param {Object} props - Component props
 * @param {number|string} props.computerId - ID of the target computer
 * @param {Object} props.computer - Computer data object
 * @param {string} props.computer.name - Name of the computer
 * @param {boolean} [props.isOnline=false] - Whether the computer is currently online
 * @returns {JSX.Element} The rendered component
 */
const ComputerConsole = ({ computerId, computer, isOnline = false }) => {
    const dispatch = useAppDispatch();
    /**
     * Current command input value
     * @type {string}
     */
    const [commandInput, setCommandInput] = useState('');
    
    /**
     * Type of command to execute (console, script, action)
     * @type {string}
     */
    const [commandType, setCommandType] = useState('console');
    
    /**
     * Console output history
     * @type {Array<Object>}
     */
    const [output, setOutput] = useState([]);
    
    /**
     * Whether a command is currently being executed
     * @type {boolean}
     */
    const [isLoading, setIsLoading] = useState(false);
    
    /**
     * Current error message, if any
     * @type {string|null}
     */
    const [error, setError] = useState(null);
    
    const commandHistory = useAppSelector(selectCommandHistory);
    
    const outputEndRef = useRef(null);
    const localIdCounter = useRef(0);

    /**
     * Scrolls the console output to the bottom
     * 
     * @function scrollToBottom
     * @returns {void}
     */
    const scrollToBottom = useCallback(() => {
        outputEndRef.current?.scrollIntoView();
    }, []);

    /**
     * Filtered command results for the current computer
     * 
     * @type {Array<Object>}
     */
    const computerCommandResults = React.useMemo(() => {
        return computerId ? commandHistory[computerId] || [] : [];
    }, [commandHistory, computerId]);

    /**
     * Processes command results into formatted console entries
     * 
     * Creates formatted output entries from raw command results including:
     * - Command input lines with timestamps
     * - Command output (stdout)
     * - Error output (stderr)
     * - Exit status information
     * - Session separators
     * 
     * @effect
     * @listens computerCommandResults
     * @listens computerId
     */
    useEffect(() => {
        const allEntries = [];
        computerCommandResults.forEach((result, index) => {
            const timestamp = result.timestamp ? new Date(result.timestamp) : new Date();
            const resultData = result.result || {};
            const stdout = (result.type === 'console' ? resultData.stdout : result.stdout) || '';
            const stderr = (result.type === 'console' ? resultData.stderr : result.stderr) || '';
            const exitCode = result.type === 'console' ? resultData.exitCode : result.exitCode;
            const sessionHasError = (stderr && stderr.trim() !== '') || !result.success;

             if (index > 0) {
                allEntries.push({
                    id: `session-${result.commandId || `res-${index}`}-separator`,
                    type: 'separator',
                    content: `--- Session ended: ${timestamp.toLocaleString()} ---`,
                    timestamp: timestamp,
                    showTimestamp: false,
                    hasError: false,
                });
            }

            const commandTypeDisplay = result.type && result.type !== 'console' ? ` [${result.type}]` : '';
            allEntries.push({
                id: `restored-${result.commandId || `res-${index}`}-input`,
                commandId: result.commandId,
                type: 'input',
                content: `$ ${commandTypeDisplay} ${result.commandText || '[Command text not available]'}`,
                timestamp: timestamp,
                showTimestamp: true,
                hasError: sessionHasError,
            });

            if (stdout && stdout.trim()) {
                allEntries.push({
                    id: `restored-${result.commandId || `res-${index}`}-stdout`,
                    commandId: result.commandId,
                    type: 'output',
                    content: stdout.trim(),
                    timestamp: timestamp,
                    showTimestamp: false,
                    hasError: sessionHasError,
                });
            }

            if (stderr && stderr.trim()) {
                allEntries.push({
                    id: `restored-${result.commandId || `res-${index}`}-stderr`,
                    commandId: result.commandId,
                    type: 'error',
                    content: `stderr: ${stderr.trim()}`,
                    timestamp: timestamp,
                    showTimestamp: false,
                    hasError: sessionHasError,
                });
            }

            allEntries.push({
                id: `restored-${result.commandId || `res-${index}`}-exit`,
                commandId: result.commandId,
                type: 'exit',
                content: `[Process exited with code: ${exitCode !== undefined && exitCode !== null ? exitCode : 'unknown'}${result.success ? ' ✓' : ' ✗'}]`,
                timestamp: timestamp,
                showTimestamp: false,
                hasError: sessionHasError,
            });
        });

        setOutput(prevOutput => {
            if (JSON.stringify(allEntries) !== JSON.stringify(prevOutput)) {
                return allEntries;
            }
            return prevOutput;
        });

    }, [computerCommandResults, computerId]);

    /**
     * Scrolls to the bottom of the console after output changes
     * 
     * @effect
     * @listens output
     */
    useEffect(() => {
        const timer = setTimeout(scrollToBottom, 100);
        return () => clearTimeout(timer);
    }, [output, scrollToBottom]);

    /**
     * Sends a command to the remote computer
     * 
     * This function:
     * 1. Validates the command and computer state
     * 2. Adds an optimistic command entry to the output
     * 3. Sends the command to the backend using CommandHandleContext
     * 4. Handles errors and updates the UI accordingly
     * 
     * @function handleSendCommand
     * @async
     * @returns {Promise<void>}
     */
    const handleSendCommand = useCallback(async () => {
        const trimmedInput = commandInput.trim();
        if (!trimmedInput || !computerId || !isOnline) {
             if (!isOnline) setError("Cannot execute commands: computer is offline.");
             else if (!trimmedInput) setError("Cannot send an empty command.");
             return;
        }

        const commandToSend = trimmedInput;
        const timestamp = new Date();
        const currentLocalId = localIdCounter.current++;

        const commandTypeDisplay = commandType !== 'console' ? ` [${commandType}]` : '';
        const optimisticHasError = false;
        const newInputLine = {
             id: `live-cmd-${currentLocalId}-input`,
             commandId: null,
             type: 'input',
             content: `$ ${commandTypeDisplay} ${commandToSend}`,
             timestamp: timestamp,
             showTimestamp: true,
             hasError: optimisticHasError
        };

        const separatorLine = {
             id: `live-separator-${currentLocalId}`,
             type: 'separator',
             content: `--- Session ended: ${timestamp.toLocaleString()} ---`,
             timestamp: timestamp,
             showTimestamp: false,
             hasError: false
        };

        setOutput(prev => [
            ...prev,
            ...(prev.length > 0 ? [separatorLine] : []),
            newInputLine
        ]);

        setCommandInput('');
        setIsLoading(true);
        setError(null);

        try {
            await dispatch(sendCommand({
                computerId,
                command: commandToSend,
                type: commandType
            })).unwrap();
        } catch (error) {
            const errorTimestamp = new Date();
            console.error("Command execution error:", error);
            const errorMessage = error.message || "Failed to execute command";
            setOutput(prev => [...prev, {
                id: `live-cmd-${currentLocalId}-send-error`,
                commandId: null,
                type: 'error',
                content: errorMessage,
                timestamp: errorTimestamp,
                showTimestamp: false,
                hasError: true
            }]);
            setError(errorMessage);
        } finally {
            setIsLoading(false);
        }
    }, [commandInput, commandType, computerId, isOnline, dispatch]);

    /**
     * Updates the command input value
     * 
     * @function handleInputChange
     * @param {React.ChangeEvent<HTMLInputElement>} e - Input change event
     * @returns {void}
     */
    const handleInputChange = (e) => setCommandInput(e.target.value);

    /**
     * Updates the command type selection
     * 
     * @function handleCommandTypeChange
     * @param {string} value - New command type value
     * @returns {void}
     */
    const handleCommandTypeChange = (value) => {
        setCommandType(value);
    };

    /**
     * Handles keyboard events in the command input
     * Executes the command when Enter is pressed
     * 
     * @function handleKeyPress
     * @param {React.KeyboardEvent} e - Keyboard event
     * @returns {void}
     */
    const handleKeyPress = (e) => {
        if (e.key === 'Enter' && !e.shiftKey && !isLoading && commandInput.trim()) {
            e.preventDefault();
            handleSendCommand();
        }
    };

    /**
     * Clears the console output and error message
     * 
     * @function clearConsole
     * @returns {void}
     */
    const clearConsole = useCallback(() => {
         setOutput([]);
         setError(null);
    }, [setOutput]);

    /**
     * Gets CSS classes for different output content types
     * 
     * @function getContentTextClasses
     * @param {string} type - Output entry type (input, output, error, exit)
     * @returns {string} CSS classes for the content
     */
    const getContentTextClasses = (type) => {
        switch (type) {
            case 'input': return 'text-blue-700 font-bold';
            case 'error': return 'text-red-700';
            case 'exit': return 'text-gray-600 italic';
            case 'output': default: return 'text-gray-800';
        }
    };

    return (
        <div className="bg-white shadow-md rounded-lg border border-gray-200 flex flex-col h-full min-h-[400px] max-h-[75vh]">
            <div className="p-3 border-b border-gray-200 flex items-center justify-between flex-shrink-0">
                <div className="flex items-center gap-2">
                    <CodeOutlined />
                    <span className="font-semibold text-gray-700">
                        Console - {computer?.name || computerId}
                    </span>
                    <span className={`ml-2 px-2 py-0.5 rounded-full text-xs font-medium ${isOnline ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                        {isOnline ? 'Online' : 'Offline'}
                    </span>
                </div>
                <button
                    onClick={clearConsole}
                    title="Clear Console Display"
                     className="p-1 rounded text-gray-500 hover:bg-red-100 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-300"
                >
                    <DeleteOutlined />
                </button>
            </div>

            <div className="flex-grow overflow-hidden flex flex-col p-2">
                {!isOnline && (
                    <div className="mb-2 p-3 border border-yellow-300 bg-yellow-50 rounded-md flex items-center gap-3 flex-shrink-0">
                         <DisconnectOutlined className="text-yellow-600 text-lg" />
                         <div>
                            <p className="font-semibold text-yellow-800">Computer Offline</p>
                            <p className="text-sm text-yellow-700">This computer is currently offline. Commands cannot be executed.</p>
                         </div>
                    </div>
                )}

                <div className="flex-grow overflow-y-auto bg-gray-50 p-3 rounded border border-gray-200 font-mono text-sm leading-relaxed mb-2">
                    {output.length === 0 && !isLoading && (
                         <div className="text-center text-gray-500 mt-4">
                            {computerCommandResults.length > 0 ? 'Command history loaded.' : 'Enter a command to start.'}
                        </div>
                    )}

                    {output.map((line) => {
                        if (line.type === 'separator') {
                            return (
                                <div key={line.id} className="text-center text-xs text-gray-500 italic border-t border-dashed border-gray-300 my-4 pt-1">
                                    {line.content}
                                </div>
                            );
                        }

                        return (
                            <div
                                key={line.id}
                                className={`flex px-1 py-0.5 rounded ${line.hasError ? 'bg-red-50' : ''}`}
                            >
                                <div className={`flex-shrink-0 w-[90px] mr-2 text-gray-500 ${line.showTimestamp ? 'visible' : 'invisible'}`}>
                                   {line.showTimestamp ? `[${line.timestamp.toLocaleTimeString()}]` : ''}
                                </div>

                                <div className={`flex-grow whitespace-pre-wrap break-all ${getContentTextClasses(line.type)}`}>
                                    {line.content}
                                </div>
                            </div>
                        );
                    })}

                    {isLoading && (
                        <div className="flex justify-center items-center gap-2 text-gray-600 py-2">
                            <Spin indicator={<LoadingOutlined style={{ fontSize: 16 }} spin />} size="small" />
                            <span className="ml-1">Running command...</span>
                        </div>
                    )}

                    <div ref={outputEndRef} />
                </div>

                {error && (
                     <div className="mb-2 p-3 border border-red-300 bg-red-50 text-red-700 rounded-md flex justify-between items-center flex-shrink-0">
                        <span>Operation Error: {error}</span>
                        <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">
                            <CloseOutlined />
                        </button>
                    </div>
                )}

                <div className="flex gap-2 flex-shrink-0">
                    <Select
                        defaultValue="console"
                        value={commandType}
                        onChange={handleCommandTypeChange}
                        style={{ width: '90px' }}
                        disabled={isLoading || !isOnline}
                    >
                        <Option value="console">Console</Option>
                        <Option value="script" disabled>Script</Option>
                        <Option value="action" disabled>Action</Option>
                    </Select>

                    <input
                        type="text"
                        placeholder={isOnline ? "Enter command..." : "Computer offline - commands disabled"}
                        value={commandInput}
                        onChange={handleInputChange}
                        onKeyPress={handleKeyPress}
                        disabled={isLoading || !isOnline}
                        className="flex-grow px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm disabled:bg-gray-100 font-mono"
                        aria-label="Command Input"
                    />

                    <button
                        type="button"
                        onClick={handleSendCommand}
                        disabled={isLoading || !commandInput.trim() || !isOnline}
                        title={isOnline ? "Send Command (Enter)" : "Computer is offline"}
                        className="inline-flex items-center justify-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {isLoading
                            ? <Spin indicator={<LoadingOutlined style={{ fontSize: 18, color: 'white' }} spin />} size="small" />
                            : <SendOutlined />
                        }
                        <span className="sr-only">Send Command</span>
                    </button>
                </div>
            </div>
        </div>
    );
};

export default ComputerConsole;
