import React from 'react';
import { Card, Button, Space, Popconfirm, message, Tooltip, Badge, Tag, Typography, Row, Col, Divider, Progress } from 'antd';
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
  DatabaseOutlined
} from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext';
import computerService from '../../services/computer.service';

const { Text, Title } = Typography;

// Export the same card style for RoomLayout to use
export const cardStyle = {
  height: '180px',
  width: '100%', // Ensure 100% width to fill the grid cell
  overflow: 'hidden',
  borderRadius: '8px',
  boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
  display: 'flex',
  flexDirection: 'column'
};

const ComputerCard = ({ computer, onEdit, onView, onRefresh, simplified = false }) => {
  const { isAdmin, hasRoomAccess } = useAuth();

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

  // Format timestamp to a readable date and time
  const formatTimestamp = (timestamp) => {
    if (!timestamp) return 'Never';
    const date = new Date(timestamp);
    return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
  };

  // Calculate time since last seen
  const getTimeSinceLastSeen = () => {
    if (!computer.last_seen) return 'Never';
    
    const now = new Date();
    const lastSeen = new Date(computer.last_seen);
    const diffMs = now - lastSeen;
    
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 60) return `${diffMins}m ago`;
    
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  };

  // Determine online status based on last_seen
  const isOnline = computer.last_seen && 
    (new Date() - new Date(computer.last_seen) < 5 * 60 * 1000); // 5 minutes threshold

  // Random value for demo purposes
  const getRandomUsage = () => Math.floor(Math.random() * 100);
  const cpuUsage = getRandomUsage();
  const ramUsage = getRandomUsage();
  const diskUsage = getRandomUsage();

  // Get status color
  const getStatusColor = (value) => {
    if (value < 60) return '#52c41a'; // Green
    if (value < 80) return '#faad14'; // Yellow
    return '#f5222d'; // Red
  };

  // Render a simplified version for the RoomLayout view
  if (simplified) {
    return (
      <Card
        hoverable
        size="small"
        style={{
          ...cardStyle,
          border: isOnline ? '1px solid #52c41a' : '1px solid #d9d9d9',
          height: '180px', // Ensure consistent height with empty cells
          width: '100%',   // Ensure card uses 100% of container width
          maxWidth: '100%' // Prevent expansion beyond container
        }}
        title={
          <div style={{ display: 'flex', alignItems: 'center', fontSize: '12px', width: '100%' }}>
            <Badge status={isOnline ? "success" : "default"} style={{ fontSize: '10px' }} />
            <span style={{ marginLeft: '4px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '12px', flex: 1 }}>
              {computer.name}
            </span>
          </div>
        }
        className="computer-card"
        extra={
          <div style={{ display: 'flex', flexShrink: 0 }}>
            <Tooltip title="View Computer">
              <Button 
                type="text" 
                icon={<DesktopOutlined style={{ fontSize: '12px' }} />} 
                size="small"
                onClick={() => onView(computer.id)} 
              />
            </Tooltip>
            
            {hasComputerAccess && (
              <Tooltip title="Edit Computer">
                <Button 
                  type="text" 
                  icon={<EditOutlined style={{ fontSize: '12px' }} />} 
                  size="small"
                  onClick={() => onEdit(computer)} 
                />
              </Tooltip>
            )}
            
            {isAdmin && (
              <Tooltip title="Delete Computer">
                <Popconfirm
                  title="Delete this computer?"
                  onConfirm={handleDelete}
                  okText="Yes"
                  cancelText="No"
                >
                  <Button type="text" danger icon={<DeleteOutlined style={{ fontSize: '12px' }} />} size="small" />
                </Popconfirm>
              </Tooltip>
            )}
          </div>
        }
        styles={{
          body: { 
            flex: 1, 
            display: 'flex', 
            flexDirection: 'column', 
            justifyContent: 'space-between', 
            padding: '8px',
            width: '100%',
            overflow: 'hidden'
          },
          header: {
            padding: '0 12px',
            minHeight: '32px',
            fontSize: '12px',
            width: '100%',
            display: 'flex',
            alignItems: 'center',
            overflow: 'hidden'
          }
        }}
      >
        <div style={{ overflow: 'hidden', width: '100%' }}>
          <Row gutter={[4, 4]}>
            <Col span={12}>
              <Tooltip title="IP Address">
                <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                  <GlobalOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                    {computer.ip_address || 'No IP'}
                  </span>
                </p>
              </Tooltip>
            </Col>
            <Col span={12}>
              <Tooltip title="Last Seen">
                <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                  <ClockCircleOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                    {getTimeSinceLastSeen()}
                  </span>
                </p>
              </Tooltip>
            </Col>
            <Col span={12}>
              <Tooltip title="CPU Info">
                <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                  <InfoCircleOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                    {computer.cpu_info ? computer.cpu_info.split(' ').slice(0, 2).join(' ') : 'No CPU info'}
                  </span>
                </p>
              </Tooltip>
            </Col>
            <Col span={12}>
              <Tooltip title="RAM">
                <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                  <DatabaseOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                    {formatRAMSize(computer.total_ram)}
                  </span>
                </p>
              </Tooltip>
            </Col>
            <Col span={12}>
              <Tooltip title="Windows Version">
                <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                  <LaptopOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                    {computer.windows_version || 'No OS info'}
                  </span>
                </p>
              </Tooltip>
            </Col>
            {computer.room?.name && (
              <Col span={12}>
                <Tooltip title="Room">
                  <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                    <HomeOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                    <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                      {computer.room.name}
                    </span>
                  </p>
                </Tooltip>
              </Col>
            )}
          </Row>
        </div>
        
        {/* Usage meters */}
        <Row gutter={[4, 0]} style={{ marginTop: '4px', width: '100%' }}>
          <Col span={8}>
            <Tooltip title={`CPU: ${cpuUsage}%`}>
              <Progress 
                percent={cpuUsage} 
                size="small"
                showInfo={false}
                strokeColor={getStatusColor(cpuUsage)}
              />
              <div style={{ textAlign: 'center', fontSize: '9px' }}>CPU</div>
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`RAM: ${ramUsage}%`}>
              <Progress 
                percent={ramUsage} 
                size="small"
                showInfo={false}
                strokeColor={getStatusColor(ramUsage)}
              />
              <div style={{ textAlign: 'center', fontSize: '9px' }}>RAM</div>
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`Disk: ${diskUsage}%`}>
              <Progress 
                percent={diskUsage} 
                size="small"
                showInfo={false}
                strokeColor={getStatusColor(diskUsage)}
              />
              <div style={{ textAlign: 'center', fontSize: '9px' }}>Disk</div>
            </Tooltip>
          </Col>
        </Row>
      </Card>
    );
  }

  // Render the full detailed card for standalone view
  return (
    <Card
      hoverable
      className="computer-card"
      style={{ 
        ...cardStyle,
        height: 'auto',
        borderRadius: '10px', 
        overflow: 'hidden',
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)',
        transition: 'all 0.3s ease',
        border: isOnline ? '1px solid #52c41a' : '1px solid #f5222d',
      }}
      cover={
        <div style={{ 
          background: 'linear-gradient(135deg, #1890ff 0%, #096dd9 100%)', 
          padding: '20px',
          position: 'relative',
          height: '100px',
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
        body: { padding: 0 }
      }}
    >
      <div style={{ padding: '10px' }}>
        {/* Usage statistics */}
        <Row gutter={16} style={{ marginBottom: '20px' }}>
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

        <Divider style={{ margin: '10px 0' }} />

        {/* Computer information */}
        <Row gutter={[16, 16]}>
          <Col span={12}>
            <Space align="center">
              <GlobalOutlined />
              <Text>{computer.ip_address || 'IP not set'}</Text>
            </Space>
          </Col>
          <Col span={12}>
            <Space align="center">
              <InfoCircleOutlined />
              <Text>{computer.cpu_info || 'CPU unknown'}</Text>
            </Space>
          </Col>
          <Col span={12}>
            <Space align="center">
              <LaptopOutlined />
              <Text>{computer.windows_version || 'OS unknown'}</Text>
            </Space>
          </Col>
          <Col span={12}>
            <Space align="center">
              <ClockCircleOutlined />
              <Tooltip title={formatTimestamp(computer.last_seen)}>
                <Text>{getTimeSinceLastSeen()}</Text>
              </Tooltip>
            </Space>
          </Col>
          <Col span={12}>
            <Space align="center">
              <DatabaseOutlined />
              <Text>{formatRAMSize(computer.total_ram)}</Text>
            </Space>
          </Col>
          {computer.room?.name && (
            <Col span={12}>
              <Space align="center">
                <HomeOutlined />
                <Text>{computer.room.name}</Text>
              </Space>
            </Col>
          )}
        </Row>

        {/* Agent info */}
        <Divider style={{ margin: '10px 0' }} />
        <Row align="middle" gutter={[8, 8]}>
          <Col span={24}>
            <Space>
              <InfoCircleOutlined />
              <Text type="secondary">Agent ID: {computer.unique_agent_id || 'Not registered'}</Text>
            </Space>
          </Col>
        </Row>

        {/* Errors section */}
        {computer.errors && computer.errors.length > 0 && (
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
                <WarningOutlined style={{ color: '#ff4d4f', fontSize: '16px' }} />
                <div>
                  <Text strong style={{ color: '#ff4d4f' }}>Errors Detected:</Text>
                  <ul style={{ margin: '5px 0 0 0', paddingLeft: '20px' }}>
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