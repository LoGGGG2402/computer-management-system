import React from 'react';
import { Card, Typography, Divider, Space, Tag, Alert } from 'antd';
import { CodeOutlined, CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined } from '@ant-design/icons';

const { Text, Paragraph, Title } = Typography;

const CommandOutput = ({ 
  result, 
  command, 
  loading = false,
  commandId = null 
}) => {
  // Handle loading state
  if (loading) {
    return (
      <Card className="command-output-card">
        <Space>
          <ClockCircleOutlined spin />
          <Text>Waiting for command result...</Text>
        </Space>
        {commandId && (
          <Paragraph type="secondary" style={{ marginTop: '8px' }}>
            Command ID: {commandId}
          </Paragraph>
        )}
      </Card>
    );
  }

  // Handle no result
  if (!result && !loading) {
    return (
      <Card className="command-output-card">
        <Alert
          message="No Output"
          description="No command has been executed yet, or the result is not available."
          type="info"
          showIcon
        />
      </Card>
    );
  }

  // Format the command execution time
  const formatTimestamp = (timestamp) => {
    if (!timestamp) return 'Unknown';
    const date = new Date(timestamp);
    return date.toLocaleString();
  };

  // Process exit code
  const getExitCodeDisplay = (exitCode) => {
    if (exitCode === 0) {
      return (
        <Tag color="success" icon={<CheckCircleOutlined />}>
          Success (Exit Code: 0)
        </Tag>
      );
    } else {
      return (
        <Tag color="error" icon={<CloseCircleOutlined />}>
          Error (Exit Code: {exitCode})
        </Tag>
      );
    }
  };

  return (
    <Card 
      className="command-output-card"
      title={
        <Space>
          <CodeOutlined />
          <span>Command Result</span>
          {result.exitCode !== undefined && getExitCodeDisplay(result.exitCode)}
        </Space>
      }
    >
      {/* Command that was executed */}
      <div className="executed-command" style={{ marginBottom: '16px' }}>
        <Text strong>Executed Command:</Text>
        <div 
          style={{ 
            background: '#f5f5f5', 
            padding: '8px', 
            borderRadius: '4px',
            marginTop: '4px',
            fontFamily: 'monospace'
          }}
        >
          {command || 'Unknown command'}
        </div>
      </div>

      <Divider style={{ margin: '12px 0' }} />

      {/* Output section */}
      <div className="command-stdout">
        <Text strong>Standard Output:</Text>
        {result.stdout ? (
          <pre 
            style={{ 
              background: '#f0f0f0',
              padding: '8px', 
              borderRadius: '4px',
              marginTop: '4px',
              whiteSpace: 'pre-wrap',
              maxHeight: '300px',
              overflow: 'auto',
              fontFamily: 'monospace'
            }}
          >
            {result.stdout}
          </pre>
        ) : (
          <div 
            style={{ 
              padding: '8px', 
              borderRadius: '4px',
              marginTop: '4px',
              color: '#999',
              fontStyle: 'italic'
            }}
          >
            No standard output
          </div>
        )}
      </div>

      {/* Only show stderr if it exists */}
      {result.stderr && (
        <div className="command-stderr" style={{ marginTop: '16px' }}>
          <Text strong type="danger">Standard Error:</Text>
          <pre 
            style={{ 
              background: '#fff1f0',
              padding: '8px', 
              borderRadius: '4px',
              marginTop: '4px',
              whiteSpace: 'pre-wrap',
              maxHeight: '200px',
              overflow: 'auto',
              fontFamily: 'monospace',
              color: '#cf1322'
            }}
          >
            {result.stderr}
          </pre>
        </div>
      )}

      {/* Execution info */}
      <div className="execution-info" style={{ marginTop: '16px' }}>
        <Text type="secondary">
          Command ID: {commandId || 'Unknown'} • 
          Completed: {formatTimestamp(result.timestamp)}
        </Text>
      </div>
    </Card>
  );
};

export default CommandOutput;