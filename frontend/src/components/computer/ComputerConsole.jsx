import React, { useState, useEffect, useRef } from "react";
import { 
  Input, 
  Button, 
  Typography, 
  Card, 
  Spin, 
  Empty, 
  Alert,
  Space,
  Divider,
  Tag,
  message
} from "antd";
import {
  CodeOutlined,
  SendOutlined,
  ClearOutlined,
  LoadingOutlined
} from "@ant-design/icons";
import { useCommandHandle } from "../../contexts/CommandHandleContext";

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;

const CommandResult = ({ result }) => {
  if (!result) return null;

  const { stdout, stderr, exitCode } = result;
  const hasOutput = stdout || stderr;

  return (
    <div className="command-result">
      <div className="result-header" style={{ marginBottom: '8px' }}>
        <Text type="secondary">Exit Code: </Text>
        {exitCode === 0 ? (
          <Tag color="success">{exitCode}</Tag>
        ) : (
          <Tag color="error">{exitCode}</Tag>
        )}
      </div>
      {hasOutput ? (
        <div className="result-content">
          {stdout && (
            <div className="stdout" style={{ marginBottom: '8px' }}>
              <Text strong>Standard Output:</Text>
              <pre style={{ 
                background: '#f5f5f5', 
                padding: '8px', 
                borderRadius: '4px',
                maxHeight: '300px',
                overflow: 'auto',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-all'
              }}>
                {stdout}
              </pre>
            </div>
          )}
          {stderr && (
            <div className="stderr">
              <Text strong type="danger">Standard Error:</Text>
              <pre style={{ 
                background: '#fff1f0', 
                padding: '8px', 
                borderRadius: '4px',
                maxHeight: '300px',
                overflow: 'auto',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-all'
              }}>
                {stderr}
              </pre>
            </div>
          )}
        </div>
      ) : (
        <Text type="secondary">No output returned from command</Text>
      )}
    </div>
  );
};

const ComputerConsole = ({ computerId, isOnline }) => {
  const [command, setCommand] = useState("");
  const [isExecuting, setIsExecuting] = useState(false);
  const [commandHistory, setCommandHistory] = useState([]);
  const [historyIndex, setHistoryIndex] = useState(-1);
  const { sendCommand, commandResults, clearResult } = useCommandHandle();
  const textAreaRef = useRef(null);

  useEffect(() => {
    // Focus the command input when the component mounts
    if (textAreaRef.current) {
      textAreaRef.current.focus();
    }
  }, []);

  // Get the command result for this specific computer
  const result = computerId ? commandResults[computerId] : null;

  const handleSubmit = async () => {
    if (!command.trim()) return;
    if (!isOnline) {
      message.error("Cannot execute command: Computer is offline");
      return;
    }

    setIsExecuting(true);
    try {
      // Add the command to history
      const newCommandHistory = [...commandHistory, command];
      setCommandHistory(newCommandHistory);
      setHistoryIndex(newCommandHistory.length);

      // Send the command
      await sendCommand(computerId, command);
      setCommand("");
    } catch (error) {
      console.error("Command execution failed:", error);
      message.error(`Command failed: ${error.message}`);
    } finally {
      setIsExecuting(false);
    }
  };

  const handleClear = () => {
    clearResult(computerId);
  };

  const handleKeyDown = (e) => {
    // Handle arrow up/down for command history
    if (e.key === "ArrowUp") {
      e.preventDefault();
      if (historyIndex > 0) {
        const newIndex = historyIndex - 1;
        setHistoryIndex(newIndex);
        setCommand(commandHistory[newIndex]);
      }
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      if (historyIndex < commandHistory.length - 1) {
        const newIndex = historyIndex + 1;
        setHistoryIndex(newIndex);
        setCommand(commandHistory[newIndex]);
      } else {
        // Clear command if we're at the end of history
        setHistoryIndex(commandHistory.length);
        setCommand("");
      }
    } else if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSubmit();
    }
  };

  return (
    <div className="computer-console">
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={4}>
          <CodeOutlined /> Command Console
        </Title>
        <Button 
          icon={<ClearOutlined />} 
          onClick={handleClear}
          disabled={!result}
        >
          Clear Output
        </Button>
      </div>

      {!isOnline && (
        <Alert 
          message="Computer is offline" 
          description="Unable to execute commands while the computer is offline." 
          type="warning" 
          showIcon 
          style={{ marginBottom: 16 }} 
        />
      )}

      <Card className="console-card" style={{ marginBottom: 16 }}>
        <div className="command-input" style={{ marginBottom: 16 }}>
          <TextArea
            ref={textAreaRef}
            value={command}
            onChange={(e) => setCommand(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter command to execute..."
            autoSize={{ minRows: 2, maxRows: 6 }}
            disabled={isExecuting || !isOnline}
            style={{ marginBottom: 8 }}
          />
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              type="primary"
              icon={isExecuting ? <LoadingOutlined /> : <SendOutlined />}
              onClick={handleSubmit}
              loading={isExecuting}
              disabled={!command.trim() || !isOnline}
            >
              {isExecuting ? "Executing..." : "Execute"}
            </Button>
          </div>
        </div>
        
        <Divider orientation="left">Command Output</Divider>
        
        <div className="command-output">
          {isExecuting ? (
            <div style={{ textAlign: "center", padding: 20 }}>
              <Spin tip="Executing command..." />
            </div>
          ) : result ? (
            <CommandResult result={result} />
          ) : (
            <Empty description="No command output to display" />
          )}
        </div>
      </Card>

      <Card title="Command History" size="small">
        {commandHistory.length > 0 ? (
          <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {commandHistory.map((cmd, index) => (
              <li 
                key={index} 
                style={{ 
                  padding: "4px 0", 
                  cursor: "pointer", 
                  borderBottom: index < commandHistory.length - 1 ? "1px solid #f0f0f0" : "none"
                }}
                onClick={() => {
                  setCommand(cmd);
                  textAreaRef.current?.focus();
                }}
              >
                <code>{cmd}</code>
              </li>
            ))}
          </ul>
        ) : (
          <Empty description="No command history" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        )}
      </Card>
    </div>
  );
};

export default ComputerConsole;