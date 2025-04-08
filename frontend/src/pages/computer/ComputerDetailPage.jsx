import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, Typography, Row, Col, Descriptions, Tag, Skeleton, Button, Space, Alert, Divider } from 'antd';
import { ArrowLeftOutlined, DesktopOutlined, CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import computerService from '../../services/computer.service';
import { useSocket } from '../../contexts/SocketContext';
import CommandInput from '../../components/computer/CommandInput';
import CommandOutput from '../../components/computer/CommandOutput';

const { Title, Text } = Typography;

const ComputerDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const [computer, setComputer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { subscribeToRooms, computerStatuses, getComputerStatus, getCommandResult } = useSocket();
  
  // State for command handling
  const [currentCommand, setCurrentCommand] = useState(null);
  const [commandText, setCommandText] = useState('');
  const [commandLoading, setCommandLoading] = useState(false);
  
  // Fetch computer data
  useEffect(() => {
    const fetchComputer = async () => {
      try {
        setLoading(true);
        const data = await computerService.getComputerById(id);
        setComputer(data);
        
        // Subscribe to room WebSocket updates
        if (data.room_id) {
          subscribeToRooms([data.room_id]);
        }
      } catch (err) {
        console.error('Error fetching computer:', err);
        setError('Failed to load computer details. Please try again later.');
      } finally {
        setLoading(false);
      }
    };
    
    if (id) {
      fetchComputer();
    }
    
    // Cleanup when component unmounts
    return () => {
      setComputer(null);
    };
  }, [id, subscribeToRooms]);
  
  // Get real-time status
  const getStatus = () => {
    if (!computer) return { status: 'unknown', cpuUsage: 0, ramUsage: 0 };
    return getComputerStatus(computer.id);
  };
  
  // Format last seen time
  const formatLastSeen = (timestamp) => {
    if (!timestamp) return 'Never';
    const date = new Date(timestamp);
    return date.toLocaleString();
  };
  
  // Handle command submission
  const handleCommandSent = (commandId, command) => {
    setCurrentCommand(commandId);
    setCommandText(command);
    setCommandLoading(true);
  };
  
  // Get command result from socket context
  const commandResult = currentCommand ? getCommandResult(currentCommand) : null;
  
  // Update loading state when command result is received
  useEffect(() => {
    if (commandResult && commandLoading) {
      setCommandLoading(false);
    }
  }, [commandResult, commandLoading]);
  
  // Render status tag
  const renderStatusTag = (status) => {
    switch (status) {
      case 'online':
        return <Tag color="success" icon={<CheckCircleOutlined />}>Online</Tag>;
      case 'offline':
        return <Tag color="error" icon={<CloseCircleOutlined />}>Offline</Tag>;
      default:
        return <Tag color="default">Unknown</Tag>;
    }
  };
  
  // Go back handler
  const handleGoBack = () => {
    navigate(-1);
  };
  
  // Current status
  const currentStatus = getStatus();
  
  if (loading) {
    return (
      <div className="computer-detail-page">
        <Skeleton active paragraph={{ rows: 10 }} />
      </div>
    );
  }
  
  if (error) {
    return (
      <div className="computer-detail-page">
        <Alert
          message="Error"
          description={error}
          type="error"
          showIcon
        />
        <Button 
          onClick={handleGoBack} 
          icon={<ArrowLeftOutlined />} 
          style={{ marginTop: '16px' }}
        >
          Go Back
        </Button>
      </div>
    );
  }
  
  if (!computer) {
    return (
      <div className="computer-detail-page">
        <Alert
          message="Computer Not Found"
          description="The requested computer could not be found."
          type="warning"
          showIcon
        />
        <Button 
          onClick={handleGoBack} 
          icon={<ArrowLeftOutlined />} 
          style={{ marginTop: '16px' }}
        >
          Go Back
        </Button>
      </div>
    );
  }
  
  return (
    <div className="computer-detail-page">
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        {/* Header with back button */}
        <div className="page-header">
          <Space>
            <Button 
              onClick={handleGoBack} 
              icon={<ArrowLeftOutlined />}
            >
              Back
            </Button>
            <Title level={2}>
              <DesktopOutlined /> {computer.name}
            </Title>
            {renderStatusTag(currentStatus.status)}
          </Space>
        </div>
        
        {/* Computer Information Card */}
        <Card title="Computer Information">
          <Descriptions bordered column={{ xxl: 4, xl: 3, lg: 3, md: 2, sm: 1, xs: 1 }}>
            <Descriptions.Item label="Status">
              {renderStatusTag(currentStatus.status)}
            </Descriptions.Item>
            <Descriptions.Item label="Last Seen">
              {formatLastSeen(computer.last_seen)}
            </Descriptions.Item>
            <Descriptions.Item label="CPU Usage">
              <Text>{currentStatus.cpuUsage || 0}%</Text>
            </Descriptions.Item>
            <Descriptions.Item label="RAM Usage">
              <Text>{currentStatus.ramUsage || 0}%</Text>
            </Descriptions.Item>
            <Descriptions.Item label="Room">
              {computer.room?.name || 'Unknown'}
            </Descriptions.Item>
            <Descriptions.Item label="Position">
              X: {computer.pos_x}, Y: {computer.pos_y}
            </Descriptions.Item>
            <Descriptions.Item label="Agent ID">
              {computer.agent_id || 'None'}
            </Descriptions.Item>
            <Descriptions.Item label="Description">
              {computer.description || 'No description'}
            </Descriptions.Item>
          </Descriptions>
        </Card>
        
        <Divider />
        
        {/* Command Section */}
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <CommandInput 
              computerId={computer.id} 
              onCommandSent={handleCommandSent}
              disabled={currentStatus.status !== 'online'}
            />
          </Col>
          <Col xs={24} lg={12}>
            <CommandOutput 
              result={commandResult} 
              command={commandText}
              loading={commandLoading}
              commandId={currentCommand}
            />
          </Col>
        </Row>
      </Space>
    </div>
  );
};

export default ComputerDetailPage;