/**
 * @fileoverview Computer Card component for displaying detailed computer information
 * 
 * This component renders a detailed card view of a computer in the system,
 * showing its status (online/offline), resource usage (CPU, RAM, disk),
 * hardware specifications, and location information. It also provides
 * admin users with the ability to delete computers from the system.
 */
import React from 'react';
import { Card, Button, Space, Popconfirm, message, Tooltip, Badge, Tag, Typography, Row, Col, Divider, Progress } from 'antd'; 
import { 
  DeleteOutlined, 
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  LaptopOutlined,
  DatabaseOutlined,
  HddOutlined,
  RocketOutlined,
  ExclamationCircleOutlined
} from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext'; 
import computerService from '../../services/computer.service'; 
import { useNavigate } from 'react-router-dom';
import { useFormatting } from '../../hooks/useFormatting'; // Import useFormatting

const { Text, Title } = Typography;

/**
 * Default styling for computer cards
 * @constant {Object} cardStyle - Base styling for computer cards
 */
export const cardStyle = {
  height: '180px',
  width: '100%', 
  overflow: 'hidden',
  borderRadius: '8px',
  boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
  display: 'flex',
  flexDirection: 'column'
};

/**
 * Computer Card Component
 * 
 * Displays detailed information about a computer including:
 * - Online/offline status
 * - Resource usage metrics (CPU, RAM, disk)
 * - Hardware specifications
 * - Network information
 * - Room assignment
 * - Error indicators
 * 
 * @component
 * @param {Object} props - Component props
 * @param {Object} props.computer - Computer data object
 * @param {number} props.computer.id - Computer ID
 * @param {string} props.computer.name - Computer name
 * @param {string} props.computer.ip_address - IP address
 * @param {string} props.computer.cpu_info - CPU information
 * @param {string} props.computer.gpu_info - GPU information
 * @param {string} props.computer.os_info - Operating system information
 * @param {number} props.computer.total_ram - Total RAM in bytes
 * @param {number} props.computer.total_disk_space - Total disk space in bytes
 * @param {string} props.computer.last_update - Timestamp of last update
 * @param {boolean} props.computer.have_active_errors - Whether computer has active errors
 * @param {Object} props.computer.room - Room assignment information
 * @param {boolean} props.isOnline - Whether the computer is currently online
 * @param {number} props.cpuUsage - Current CPU usage percentage (0-100)
 * @param {number} props.ramUsage - Current RAM usage percentage (0-100)
 * @param {number} props.diskUsage - Current disk usage percentage (0-100)
 * @param {Function} props.onRefresh - Callback function to refresh the parent component
 * @returns {JSX.Element|null} The rendered component or null if no computer data
 */
const ComputerCard = React.memo(({ 
  computer, 
  isOnline, 
  cpuUsage,   
  ramUsage,   
  diskUsage,  
  onRefresh,
}) => {
  const { isAdmin } = useAuth(); 
  const navigate = useNavigate();
  const { formatRAMSize, formatDiskSize, formatTimestamp, getTimeAgo, getStatusColor } = useFormatting(); // Use formatting hook

  /**
   * Handles computer deletion
   * 
   * Deletes the computer from the system and triggers a refresh
   * Only available to admin users
   * 
   * @async
   * @function handleDelete
   * @returns {Promise<void>}
   */
  const handleDelete = async () => {
    if (!computer?.id) return;
    try {
      await computerService.deleteComputer(computer.id);
      message.success('Computer deleted successfully');
      // Navigate back to the room page if room exists, otherwise go to computers list
      if (computer.room?.id) {
        navigate(`/rooms/${computer.room.id}`);
      } else {
        navigate('/computers');
      }
      if (onRefresh) onRefresh(); 
    } catch (error) {
      message.error('Failed to delete computer');
      console.error('Error deleting computer:', error);
    }
  };

  if (!computer) return null;

  /**
   * Gets a human-readable string representing time since computer was last seen
   * 
   * @function getTimeSinceLastSeen
   * @returns {string} Time since last seen
   */
  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return 'Never';
    return getTimeAgo(computer.last_update); // Use hook function
  };

  return (
    <Card
      hoverable
      className="computer-card-detailed"
      style={{ 
        ...cardStyle,
        height: 'auto', 
        border: isOnline ? '1px solid #52c41a' : '1px solid #f5222d', 
      }}
      cover={
        <div style={{ 
          background: 'linear-gradient(135deg, #1890ff 0%, #096dd9 100%)', 
          padding: '20px', position: 'relative', height: '100px',
          display: 'flex', alignItems: 'center'
        }}>
          <LaptopOutlined style={{ fontSize: '48px', color: 'white', marginRight: '15px' }} />
          <div>
            <Title level={4} style={{ color: 'white', margin: '0' }}>{computer.name}</Title>
            <Space>
              <Badge status={isOnline ? "success" : "error"} /> 
              <Text style={{ color: 'white' }}>
                {isOnline ? "Online" : "Offline"} 
              </Text>
              {computer.have_active_errors && (
                <Tooltip title="Computer has errors requiring attention">
                  <ExclamationCircleOutlined 
                    style={{ 
                      color: '#ff4d4f', 
                      fontSize: '16px',
                      backgroundColor: 'white',
                      borderRadius: '50%',
                      padding: '2px',
                      cursor: 'pointer'
                    }}
                    onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/computers/${computer.id}?tab=errors`);
                    }}
                  />
                </Tooltip>
              )}
              {computer.room?.name && (
                <Tag color="blue">
                  <HomeOutlined /> {computer.room.name}
                </Tag>
              )}
            </Space>
          </div>
          <div style={{ position: 'absolute', right: '15px', top: '15px' }}>
            <Space>
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
      <div style={{ padding: '20px' }}>
        <Row gutter={16} style={{ marginBottom: '20px', textAlign: 'center' }}>
          <Col span={8}>
            <Tooltip title={`CPU Usage: ${cpuUsage}%`}>
              <Progress 
                type="dashboard" percent={cpuUsage} size={80} 
                format={() => 'CPU'} strokeColor={getStatusColor(cpuUsage)} // Use hook
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`RAM Usage: ${ramUsage}%`}>
              <Progress 
                type="dashboard" percent={ramUsage} size={80} 
                format={() => 'RAM'} strokeColor={getStatusColor(ramUsage)} // Use hook
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`Disk Usage: ${diskUsage}%`}>
              <Progress 
                type="dashboard" percent={diskUsage} size={80} 
                format={() => 'Disk'} strokeColor={getStatusColor(diskUsage)} // Use hook
              />
            </Tooltip>
          </Col>
        </Row>

        <Divider style={{ margin: '10px 0 20px 0' }} />

        <Row gutter={[16, 16]}>
           <Col xs={24} sm={12}>
            <Space align="center">
              <GlobalOutlined />
              <Text>{computer.ip_address || 'No IP'}</Text>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <ClockCircleOutlined />
              <Tooltip title={`Specific time: ${formatTimestamp(computer.last_update)}`}> {/* Use hook */}
                <Text>Last seen: {getTimeSinceLastSeen()}</Text>
              </Tooltip>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <InfoCircleOutlined />
              <Tooltip title={computer.cpu_info || 'CPU Unknown'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.cpu_info || 'CPU Unknown'}
                 </Text>
              </Tooltip>
            </Space>
          </Col>
           <Col xs={24} sm={12}>
            <Space align="center">
              <LaptopOutlined />
               <Tooltip title={computer.os_info || 'OS Unknown'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                   {computer.os_info || 'OS Unknown'}
                 </Text>
               </Tooltip>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <DatabaseOutlined />
              <Text>RAM: {formatRAMSize(computer.total_ram)}</Text> {/* Use hook */}
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <HddOutlined />
              <Text>Disk: {formatDiskSize(computer.total_disk_space)}</Text> {/* Use hook */}
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <RocketOutlined />
              <Tooltip title={computer.gpu_info || 'GPU Unknown'}>
                <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {computer.gpu_info || 'GPU Unknown'}
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

        <Divider style={{ margin: '20px 0 10px 0' }} />
        <Row align="middle" gutter={[8, 8]}>
          <Col span={24}>
            <Space>
              <InfoCircleOutlined />
              <Text type="secondary">Agent ID: {computer.unique_agent_id || 'Not registered'}</Text>
            </Space>
          </Col>
        </Row>
      </div>
    </Card>
  );
}); 

export default ComputerCard;
