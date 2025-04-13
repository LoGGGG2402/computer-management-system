import React from 'react';
import { Card, Button, Space, Popconfirm, message, Tooltip, Badge, Tag, Typography, Row, Col, Divider, Progress, Popover } from 'antd';
import { 
  EditOutlined, 
  DeleteOutlined, 
  DesktopOutlined, 
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  WarningOutlined,
  LaptopOutlined,
  DatabaseOutlined,
  CodeOutlined,
  HddOutlined,
  RocketOutlined
} from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext';
import { useSocket } from '../../contexts/SocketContext';
import { useCommandHandle } from '../../contexts/CommandHandleContext';
import computerService from '../../services/computer.service';

const { Text, Title } = Typography;

// Define card style locally if needed, or import from a shared style file
const cardStyle = {
  // Removed fixed height for detailed card
  width: '100%', 
  overflow: 'hidden',
  borderRadius: '10px', // Kept detailed view styling
  boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)', // Kept detailed view styling
  display: 'flex',
  flexDirection: 'column',
  transition: 'all 0.3s ease' // Kept detailed view styling
};

const ComputerCard = ({ computer, onEdit, onView, onRefresh }) => {
  const { isAdmin, hasRoomAccess } = useAuth();
  const { getComputerStatus } = useSocket();
  const { commandResults, clearResult } = useCommandHandle();

  // Get real-time status data
  const statusData = getComputerStatus(computer?.id);
  
  // Get command result for this computer
  const commandResult = computer?.id ? commandResults[computer.id] : null;
  
  // Check if user has access to this computer
  const hasComputerAccess = computer && computer.room_id && 
    (isAdmin || hasRoomAccess(computer.room_id));

  const handleDelete = async () => {
    try {
      await computerService.deleteComputer(computer.id);
      message.success('Computer deleted successfully');
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error('Failed to delete computer');
      console.error('Error deleting computer:', error);
    }
  };

  if (!computer) return null;

  // Format RAM size to be more readable
  const formatRAMSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };

  // Format disk space size to be more readable
  const formatDiskSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };

  // Format timestamp to a readable date and time
  const formatTimestamp = (timestamp) => {
    if (!timestamp) return 'Never';
    const date = new Date(timestamp);
    return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
  };

  // Get a human-readable time from timestamp
  const getTimeAgo = (timestamp) => {
    if (!timestamp) return 'Never';
    
    const now = new Date();
    const time = new Date(timestamp);
    const diffMs = now - time;
    
    const diffSecs = Math.floor(diffMs / 1000);
    if (diffSecs < 60) return `${diffSecs}s ago`;
    
    const diffMins = Math.floor(diffSecs / 60);
    if (diffMins < 60) return `${diffMins}m ago`;
    
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  };

  // Calculate time since last seen
  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return 'Never';
    
    const now = new Date();
    const lastSeen = new Date(computer.last_update);
    const diffMs = now - lastSeen;
    
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 60) return `${diffMins}m ago`;
    
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  };

  // Determine online status based on real-time data or fallback to last_update
  const isOnline = statusData?.status === 'online' || 
    (computer.status === 'online') || 
    (computer.last_update && (new Date() - new Date(computer.last_update) < 5 * 60 * 1000)); // 5 minutes threshold

  // Get real-time CPU, RAM, and Disk usage or use defaults
  const cpuUsage = statusData?.cpuUsage ?? 0;
  const ramUsage = statusData?.ramUsage ?? 0;
  const diskUsage = statusData?.diskUsage ?? 0; 

  // Get status color
  const getStatusColor = (value) => {
    if (value < 60) return '#52c41a'; // Green
    if (value < 80) return '#faad14'; // Yellow
    return '#f5222d'; // Red
  };

  // Render command result popover content
  const renderCommandResultContent = () => {
    if (!commandResult) return null;
    
    return (
      <div style={{ maxWidth: '300px' }}>
        <div style={{ marginBottom: '8px' }}>
          <Text strong>Command Result</Text>
          <Button 
            size="small" 
            type="text" 
            style={{ float: 'right', padding: '0' }}
            onClick={() => clearResult(computer.id)}
          >
            Clear
          </Button>
        </div>
        
        <div style={{ marginBottom: '8px' }}>
          <Tag color={commandResult.exitCode === 0 ? 'success' : 'error'}>
            Exit Code: {commandResult.exitCode}
          </Tag>
          <Text type="secondary" style={{ fontSize: '12px', marginLeft: '8px' }}>
            {getTimeAgo(commandResult.timestamp)}
          </Text>
        </div>
        
        {commandResult.stdout && (
          <div style={{ marginBottom: '8px' }}>
            <Text strong>Output:</Text>
            <div style={{ 
              background: '#f5f5f5', 
              padding: '8px', 
              borderRadius: '4px',
              maxHeight: '150px',
              overflowY: 'auto',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              fontSize: '12px'
            }}>
              {commandResult.stdout}
            </div>
          </div>
        )}
        
        {commandResult.stderr && (
          <div>
            <Text strong type="danger">Error:</Text>
            <div style={{ 
              background: '#fff2f0', 
              padding: '8px', 
              borderRadius: '4px',
              maxHeight: '150px',
              overflowY: 'auto',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              fontSize: '12px',
              color: '#ff4d4f'
            }}>
              {commandResult.stderr}
            </div>
          </div>
        )}
      </div>
    );
  };

  // Render the full detailed card
  return (
    <Card
      hoverable
      className="computer-card-detailed" // Added specific class if needed
      style={{ 
        ...cardStyle,
        height: 'auto', // Override fixed height
        border: isOnline ? '1px solid #52c41a' : '1px solid #f5222d',
      }}
      cover={
        <div style={{ 
          background: 'linear-gradient(135deg, #1890ff 0%, #096dd9 100%)', 
          padding: '20px',
          position: 'relative',
          height: '100px', // Fixed height for cover
          display: 'flex',
          alignItems: 'center'
        }}>
          <LaptopOutlined style={{ fontSize: '48px', color: 'white', marginRight: '15px' }} />
          <div>
            <Title level={4} style={{ color: 'white', margin: '0' }}>{computer.name}</Title>
            <Space>
              <Badge status={isOnline ? "success" : "error"} />
              <Text style={{ color: 'white' }}>
                {isOnline ? "Online" : "Offline"}
              </Text>
              {computer.room?.name && (
                <Tag color="blue">
                  <HomeOutlined /> {computer.room.name}
                </Tag>
              )}
              
              {/* Command result indicator for full card */}
              {commandResult && (
                <Popover
                  content={renderCommandResultContent()}
                  title={null}
                  trigger="hover"
                  placement="bottom"
                >
                  <Tag 
                    icon={<CodeOutlined />} 
                    color={commandResult.exitCode === 0 ? 'success' : 'error'}
                  >
                    Command Result
                  </Tag>
                </Popover>
              )}
            </Space>
          </div>
          <div style={{ position: 'absolute', right: '15px', top: '15px' }}>
            <Space>
              {/* View button */}
              <Tooltip title="View details">
                <Button 
                  type="text" 
                  shape="circle" 
                  icon={<DesktopOutlined style={{ color: 'white', fontSize: '18px' }} />} 
                  onClick={() => onView(computer.id)}
                />
              </Tooltip>
              
              {/* Edit button - only for admin or users with access */}
              {hasComputerAccess && (
                <Tooltip title="Edit computer">
                  <Button 
                    type="text" 
                    shape="circle" 
                    icon={<EditOutlined style={{ color: 'white', fontSize: '18px' }} />} 
                    onClick={() => onEdit(computer)}
                  />
                </Tooltip>
              )}
              
              {/* Delete button - only for admin */}
              {isAdmin && (
                <Tooltip title="Delete computer">
                  <Popconfirm
                    title="Are you sure you want to delete this computer?"
                    onConfirm={handleDelete}
                    okText="Yes"
                    cancelText="No"
                  >
                    <Button 
                      type="text" 
                      danger
                      shape="circle" 
                      icon={<DeleteOutlined style={{ color: 'white', fontSize: '18px' }} />}
                    />
                  </Popconfirm>
                </Tooltip>
              )}
            </Space>
          </div>
        </div>
      }
      styles={{
        body: { padding: 0 } // Reset body padding for custom layout inside
      }}
    >
      <div style={{ padding: '20px' }}> {/* Add padding inside the body */}
        {/* Usage statistics */}
        <Row gutter={16} style={{ marginBottom: '20px', textAlign: 'center' }}>
          <Col span={8}>
            <Tooltip title={`CPU Usage: ${cpuUsage}%`}>
              <Progress 
                type="dashboard" 
                percent={cpuUsage} 
                width={80} 
                format={() => 'CPU'} 
                strokeColor={getStatusColor(cpuUsage)}
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`RAM Usage: ${ramUsage}%`}>
              <Progress 
                type="dashboard" 
                percent={ramUsage} 
                width={80} 
                format={() => 'RAM'} 
                strokeColor={getStatusColor(ramUsage)}
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`Disk Usage: ${diskUsage}%`}>
              <Progress 
                type="dashboard" 
                percent={diskUsage} 
                width={80} 
                format={() => 'Disk'} 
                strokeColor={getStatusColor(diskUsage)}
              />
            </Tooltip>
          </Col>
        </Row>

        <Divider style={{ margin: '10px 0 20px 0' }} />

        {/* Computer information */}
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12}>
            <Space align="center">
              <GlobalOutlined />
              <Text>{computer.ip_address || 'IP not set'}</Text>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <ClockCircleOutlined />
              <Tooltip title={formatTimestamp(computer.last_update)}>
                <Text>Last seen: {getTimeSinceLastSeen()}</Text>
              </Tooltip>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <InfoCircleOutlined />
              <Tooltip title={computer.cpu_info || 'CPU unknown'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.cpu_info || 'CPU unknown'}
                 </Text>
              </Tooltip>
            </Space>
          </Col>
           <Col xs={24} sm={12}>
            <Space align="center">
              <LaptopOutlined />
               <Tooltip title={computer.os_info || 'OS unknown'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                   {computer.os_info || 'OS unknown'}
                 </Text>
               </Tooltip>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <DatabaseOutlined />
              <Text>RAM: {formatRAMSize(computer.total_ram)}</Text>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <HddOutlined />
              <Text>Disk: {formatDiskSize(computer.total_disk_space)}</Text>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <RocketOutlined />
              <Tooltip title={computer.gpu_info || 'GPU unknown'}>
                <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {computer.gpu_info || 'GPU unknown'}
                </Text>
              </Tooltip>
            </Space>
          </Col>
          {computer.room?.name && (
            <Col xs={24} sm={12}>
              <Space align="center">
                <HomeOutlined />
                <Text>Room: {computer.room.name}</Text>
              </Space>
            </Col>
          )}
        </Row>

        {/* Agent info */}
        <Divider style={{ margin: '20px 0 10px 0' }} />
        <Row align="middle" gutter={[8, 8]}>
          <Col span={24}>
            <Space>
              <InfoCircleOutlined />
              <Text type="secondary">Agent ID: {computer.unique_agent_id || 'Not registered'}</Text>
            </Space>
          </Col>
        </Row>

        {/* Errors section */}
        {computer.errors && Array.isArray(computer.errors) && computer.errors.length > 0 && (
          <>
            <Divider style={{ margin: '10px 0' }} />
            <div style={{ 
              backgroundColor: '#fff2f0', 
              border: '1px solid #ffccc7', 
              padding: '10px', 
              borderRadius: '4px',
              marginTop: '10px'
            }}>
              <Space align="start">
                <WarningOutlined style={{ color: '#ff4d4f', fontSize: '16px', marginTop: '3px' }} />
                <div>
                  <Text strong style={{ color: '#ff4d4f' }}>Errors Detected:</Text>
                  <ul style={{ margin: '5px 0 0 0', paddingLeft: '20px', listStyleType: 'disc' }}>
                    {computer.errors.map((error, index) => (
                      <li key={index}>
                        <Text type="danger">{error}</Text>
                      </li>
                    ))}
                  </ul>
                </div>
              </Space>
            </div>
          </>
        )}
      </div>
    </Card>
  );
};

export default ComputerCard;