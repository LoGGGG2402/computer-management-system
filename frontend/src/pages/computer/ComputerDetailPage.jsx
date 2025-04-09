import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { Card, Row, Col, Tabs, Spin, Button, message, Typography, Divider, Breadcrumb, Tag, Space, Alert } from 'antd';
import { HomeOutlined, DesktopOutlined, ArrowLeftOutlined, ReloadOutlined, CheckCircleOutlined, CloseCircleOutlined, GlobalOutlined, InfoCircleOutlined, HddOutlined, RocketOutlined } from '@ant-design/icons';
import computerService from '../../services/computer.service';
import { useSocket } from '../../contexts/SocketContext';
import { useAuth } from '../../contexts/AuthContext';
import { useCommandHandle } from '../../contexts/CommandHandleContext';
import ComputerCard from '../../components/computer/ComputerCard';
import CommandInput from '../../components/computer/CommandInput';
import CommandOutput from '../../components/computer/CommandOutput';

const { Title, Text } = Typography;

const ComputerDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { subscribeToRooms, unsubscribeFromRooms, getComputerStatus } = useSocket();
  const { commandResults } = useCommandHandle();
  const { isAdmin, hasRoomAccess } = useAuth();
  
  const [computer, setComputer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [refreshKey, setRefreshKey] = useState(0);
  
  // Command execution state
  const [currentCommand, setCurrentCommand] = useState(null);
  const [commandId, setCommandId] = useState(null);
  const [commandLoading, setCommandLoading] = useState(false);
  
  // Get real-time status if available
  const statusData = getComputerStatus(parseInt(id));
  const commandResult = commandId ? commandResults[commandId] : null;
  
  // When command result is received, update loading state
  useEffect(() => {
    if (commandLoading && commandResult) {
      setCommandLoading(false);
    }
  }, [commandResult, commandLoading]);
  
  // Load computer details when component mounts
  useEffect(() => {
    fetchComputerDetails();
    
    return () => {
      // Clean up by unsubscribing when component unmounts
      if (computer?.room_id) {
        unsubscribeFromRooms([computer.room_id]);
      }
    };
  }, [id, refreshKey]);
  
  // When computer data is loaded, subscribe to its room for real-time updates
  useEffect(() => {
    if (computer?.room_id) {
      subscribeToRooms([computer.room_id]);
    }
  }, [computer?.room_id, subscribeToRooms]);
  
  const fetchComputerDetails = async () => {
    setLoading(true);
    try {
      const response = await computerService.getComputerById(id);
      setComputer(response.data?.data || response.data);
    } catch (error) {
      console.error('Error fetching computer details:', error);
      setError('Failed to load computer details. Please try again later.');
      message.error('Failed to load computer details');
    } finally {
      setLoading(false);
    }
  };
  
  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };
  
  const handleGoBack = () => {
    navigate(-1);
  };
  
  // Handle command execution
  const handleCommandSent = (newCommandId, command) => {
    setCommandId(newCommandId);
    setCurrentCommand(command);
    setCommandLoading(true);
    
    // Switch to console tab
    setActiveTab('console');
    
    message.info(`Command sent: ${command}`);
  };
  
  // Format last seen time
  const formatLastSeen = (timestamp) => {
    if (!timestamp) return 'Never';
    const date = new Date(timestamp);
    return date.toLocaleString();
  };

  // Format disk size to be more readable
  const formatDiskSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const gb = bytes / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };
  
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
  
  // Check if user has access to execute commands
  const canExecuteCommands = computer ? 
    (isAdmin || (computer.room_id && hasRoomAccess(computer.room_id))) : false;
  
  // Define tab items
  const items = [
    {
      key: 'overview',
      label: 'Overview',
      children: (
        <div className="computer-overview">
          {computer && (
            <ComputerCard 
              computer={computer} 
              onRefresh={handleRefresh}
              onView={() => {}} // No-op since we're already in detail view
            />
          )}
        </div>
      ),
    },
    {
      key: 'details',
      label: 'System Information',
      children: (
        <div className="computer-details">
          <Card>
            <Row gutter={[16, 16]}>
              <Col span={24}>
                <Title level={4}>Hardware Information</Title>
                <Divider />
              </Col>
              <Col span={12}>
                <Text strong>CPU: </Text>
                <Text>{computer?.cpu_info || 'Unknown'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>Memory: </Text>
                <Text>
                  {computer?.total_ram 
                    ? `${(parseInt(computer.total_ram) / (1024 * 1024 * 1024)).toFixed(2)} GB`
                    : 'Unknown'}
                </Text>
              </Col>
              <Col span={12}>
                <Text strong>OS: </Text>
                <Text>{computer?.windows_version || 'Unknown'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>IP Address: </Text>
                <Text>{computer?.ip_address || 'Not set'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>Disk Space: </Text>
                <Text>{computer?.total_disk_space ? formatDiskSize(computer.total_disk_space) : 'Unknown'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>GPU: </Text>
                <Text>{computer?.gpu_info || 'Unknown'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>Agent ID: </Text>
                <Text>{computer?.unique_agent_id || 'Not registered'}</Text>
              </Col>
              <Col span={12}>
                <Text strong>Position: </Text>
                <Text>X: {computer?.pos_x || 0}, Y: {computer?.pos_y || 0}</Text>
              </Col>
            </Row>
            
            <Row gutter={[16, 16]} style={{ marginTop: '32px' }}>
              <Col span={24}>
                <Title level={4}>Current Status</Title>
                <Divider />
              </Col>
              <Col span={12}>
                <Text strong>Status: </Text>
                {renderStatusTag(statusData?.status)}
              </Col>
              <Col span={12}>
                <Text strong>CPU Usage: </Text>
                <Text>{statusData?.cpuUsage || 0}%</Text>
              </Col>
              <Col span={12}>
                <Text strong>RAM Usage: </Text>
                <Text>{statusData?.ramUsage || 0}%</Text>
              </Col>
              <Col span={12}>
                <Text strong>Disk Usage: </Text>
                <Text>{statusData?.diskUsage || 0}%</Text>
              </Col>
              <Col span={12}>
                <Text strong>Last Updated: </Text>
                <Text>
                  {statusData?.timestamp 
                    ? new Date(statusData.timestamp).toLocaleString()
                    : 'Never'}
                </Text>
              </Col>
            </Row>
            
            {/* Errors section */}
            {computer.errors && Array.isArray(computer.errors) && computer.errors.length > 0 && (
              <Row gutter={[16, 16]} style={{ marginTop: '32px' }}>
                <Col span={24}>
                  <Title level={4}>Errors</Title>
                  <Divider />
                  <Alert
                    message="Error Information"
                    description={
                      <ul style={{ margin: '5px 0 0 0', paddingLeft: '20px' }}>
                        {console.log(computer.errors)}
                        {computer.errors.map((error, index) => (
                          <li key={index}>
                            <Text type="danger">{error}</Text>
                          </li>
                        ))}
                      </ul>
                    }
                    type="error"
                    showIcon
                  />
                </Col>
              </Row>
            )}
          </Card>
        </div>
      ),
    },
    {
      key: 'console',
      label: 'Remote Console',
      children: (
        <div className="console-tab">
          <div className="console-header" style={{ marginBottom: '16px' }}>
            <Title level={4}>Remote Command Execution</Title>
            <Text type="secondary">Send commands to this computer remotely</Text>
          </div>
          
          <Row gutter={[16, 16]}>
            <Col span={24} lg={12}>
              <CommandInput 
                computerId={parseInt(id)}
                onCommandSent={handleCommandSent}
                disabled={!canExecuteCommands || statusData?.status !== 'online'}
              />
            </Col>
            <Col span={24} lg={12}>
              <CommandOutput 
                result={commandResult}
                command={currentCommand}
                loading={commandLoading}
                commandId={commandId}
              />
            </Col>
          </Row>
        </div>
      ),
    },
  ];
  
  const handleTabChange = (key) => {
    setActiveTab(key);
  };
  
  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '300px' }}>
        <Spin size="large" tip="Loading computer details..." />
      </div>
    );
  }
  
  if (error) {
    return (
      <div className="not-found">
        <Alert
          message="Error"
          description={error}
          type="error"
          showIcon
        />
        <Button type="primary" onClick={handleGoBack} style={{ marginTop: '16px' }}>
          <ArrowLeftOutlined /> Go Back
        </Button>
      </div>
    );
  }
  
  if (!computer) {
    return (
      <div className="not-found">
        <Alert 
          message="Computer Not Found"
          description="The requested computer could not be found."
          type="warning"
          showIcon
        />
        <Button type="primary" onClick={handleGoBack} style={{ marginTop: '16px' }}>
          <ArrowLeftOutlined /> Go Back
        </Button>
      </div>
    );
  }
  
  return (
    <div className="computer-detail-page">
      <div className="page-header" style={{ marginBottom: '24px' }}>
        <Breadcrumb items={[
          { title: <><HomeOutlined /> Home</>, href: '/' },
          { title: 'Rooms', href: '/rooms' },
          { title: computer.room?.name || 'Room', href: `/rooms/${computer.room_id}` },
          { title: computer.name || 'Computer' }
        ]} />
        
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px' }}>
          <div>
            <Title level={2}>
              <DesktopOutlined /> {computer.name}
            </Title>
            <Space>
              {renderStatusTag(statusData?.status)}
              <Text type="secondary">
                ID: {computer.id} &bull; Room: {computer.room?.name || 'Unknown'}
              </Text>
            </Space>
          </div>
          <div>
            <Button 
              icon={<ArrowLeftOutlined />} 
              onClick={handleGoBack} 
              style={{ marginRight: '8px' }}
            >
              Back
            </Button>
            <Button 
              type="primary" 
              icon={<ReloadOutlined />} 
              onClick={handleRefresh}
            >
              Refresh
            </Button>
          </div>
        </div>
      </div>
      
      <Tabs 
        activeKey={activeTab}
        items={items}
        onChange={handleTabChange}
        tabBarStyle={{ marginBottom: '16px' }}
      />
    </div>
  );
};

export default ComputerDetailPage;