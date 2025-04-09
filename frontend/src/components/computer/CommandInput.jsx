import React, { useState } from 'react';
import { Input, Button, Form, Card, Alert, Typography, Space } from 'antd';
import { CodeOutlined, SendOutlined, WarningOutlined } from '@ant-design/icons';
import { useCommandHandle } from '../../contexts/CommandHandleContext';

const { TextArea } = Input;
const { Text, Title } = Typography;

const CommandInput = ({ computerId, onCommandSent = () => {}, disabled = false }) => {
  const [form] = Form.useForm();
  const { sendCommand } = useCommandHandle();
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [lastCommandId, setLastCommandId] = useState(null);
  
  const handleSubmit = async (values) => {
    if (!values.command || !values.command.trim()) {
      return;
    }
    
    setLoading(true);
    setError(null);
    
    try {
      // Use the sendCommand function from CommandHandleContext
      const result = await sendCommand(computerId, values.command.trim());
      setLastCommandId(result.commandId);
      
      // Notify parent component
      onCommandSent(result.commandId, values.command);
      
      // Clear the form
      form.resetFields();
      
    } catch (error) {
      setError(error.message || 'Failed to send command. Is the agent offline?');
      console.error('Error sending command:', error);
    } finally {
      setLoading(false);
    }
  };
  
  return (
    <div className="command-input">
      <Card 
        title={
          <Space>
            <CodeOutlined />
            <span>Command Execution</span>
          </Space>
        }
        className="command-card"
      >
        {error && (
          <Alert
            message="Command Error"
            description={error}
            type="error"
            showIcon
            icon={<WarningOutlined />}
            closable
            onClose={() => setError(null)}
            style={{ marginBottom: '16px' }}
          />
        )}
        
        <Form
          form={form}
          onFinish={handleSubmit}
          layout="vertical"
        >
          <Form.Item
            name="command"
            label="Enter command to execute"
            rules={[{ required: true, message: 'Please enter a command' }]}
          >
            <TextArea
              placeholder="Enter command (e.g., dir, ls, systeminfo)"
              autoSize={{ minRows: 2, maxRows: 6 }}
              disabled={disabled || loading}
            />
          </Form.Item>
          
          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              disabled={disabled}
              icon={<SendOutlined />}
              block
            >
              Execute Command
            </Button>
          </Form.Item>
        </Form>
        
        {lastCommandId && (
          <div className="command-status">
            <Text type="secondary">Last command ID: {lastCommandId}</Text>
          </div>
        )}
        
        <div className="command-help" style={{ marginTop: '16px' }}>
          <Title level={5}>Available Commands</Title>
          <ul>
            <li><Text code>systeminfo</Text> - Display system information</li>
            <li><Text code>dir</Text> or <Text code>ls</Text> - List directory contents</li>
            <li><Text code>ipconfig</Text> or <Text code>ifconfig</Text> - Display network configuration</li>
            <li><Text code>tasklist</Text> or <Text code>ps</Text> - Show running processes</li>
            <li><Text code>echo %cd%</Text> or <Text code>pwd</Text> - Show current directory</li>
          </ul>
          <Alert
            message="Note: Commands are executed with the permissions of the agent service"
            type="info"
            showIcon
          />
        </div>
      </Card>
    </div>
  );
};

export default CommandInput;