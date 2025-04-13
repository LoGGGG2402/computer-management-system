import React from 'react';
import { Card, Button, Space, Popconfirm, message, Tooltip, Badge, Tag, Typography, Row, Col, Divider, Progress } from 'antd'; 
import { 
  DeleteOutlined, 
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  WarningOutlined,
  LaptopOutlined,
  DatabaseOutlined,
  HddOutlined,
  RocketOutlined
} from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext'; 
import computerService from '../../services/computer.service'; 

const { Text, Title } = Typography;

const cardStyle = {
  width: '100%', 
  overflow: 'hidden',
  borderRadius: '10px', 
  boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)', 
  display: 'flex',
  flexDirection: 'column',
  transition: 'all 0.3s ease' 
};

const ComputerCard = React.memo(({ 
  computer, 
  isOnline, 
  cpuUsage,   
  ramUsage,   
  diskUsage,  
  onRefresh,
}) => {
  const { isAdmin } = useAuth(); 

  const handleDelete = async () => {
    if (!computer?.id) return;
    try {
      await computerService.deleteComputer(computer.id);
      message.success('Đã xóa máy tính thành công');
      if (onRefresh) onRefresh(); 
    } catch (error) {
      message.error('Xóa máy tính thất bại');
      console.error('Lỗi khi xóa máy tính:', error);
    }
  };

  if (!computer) return null;

  const formatRAMSize = (bytes) => {
    if (!bytes) return 'Không rõ';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };

  const formatDiskSize = (bytes) => {
    if (!bytes) return 'Không rõ';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };

  const formatTimestamp = (timestamp) => {
    if (!timestamp) return 'Chưa bao giờ';
    const date = new Date(timestamp);
    return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
  };

  const getTimeAgo = (timestamp) => {
    if (!timestamp) return 'Chưa bao giờ';
    const now = new Date();
    const time = new Date(timestamp);
    const diffMs = now - time;
    const diffSecs = Math.floor(diffMs / 1000);
    if (diffSecs < 60) return `${diffSecs} giây trước`;
    const diffMins = Math.floor(diffSecs / 60);
    if (diffMins < 60) return `${diffMins} phút trước`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours} giờ trước`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} ngày trước`;
  };

  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return 'Chưa bao giờ';
    return getTimeAgo(computer.last_update); 
  };

  const getStatusColor = (value) => {
    if (value < 60) return '#52c41a'; 
    if (value < 80) return '#faad14'; 
    return '#f5222d'; 
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
                <Tooltip title="Xóa máy tính">
                  <Popconfirm
                    title="Bạn chắc chắn muốn xóa máy tính này?"
                    onConfirm={handleDelete} 
                    okText="Có"
                    cancelText="Không"
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
            <Tooltip title={`Sử dụng CPU: ${cpuUsage}%`}>
              <Progress 
                type="dashboard" percent={cpuUsage} width={80} 
                format={() => 'CPU'} strokeColor={getStatusColor(cpuUsage)}
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`Sử dụng RAM: ${ramUsage}%`}>
              <Progress 
                type="dashboard" percent={ramUsage} width={80} 
                format={() => 'RAM'} strokeColor={getStatusColor(ramUsage)}
              />
            </Tooltip>
          </Col>
          <Col span={8}>
            <Tooltip title={`Sử dụng Disk: ${diskUsage}%`}>
              <Progress 
                type="dashboard" percent={diskUsage} width={80} 
                format={() => 'Disk'} strokeColor={getStatusColor(diskUsage)}
              />
            </Tooltip>
          </Col>
        </Row>

        <Divider style={{ margin: '10px 0 20px 0' }} />

        <Row gutter={[16, 16]}>
           <Col xs={24} sm={12}>
            <Space align="center">
              <GlobalOutlined />
              <Text>{computer.ip_address || 'Chưa có IP'}</Text>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <ClockCircleOutlined />
              <Tooltip title={`Thời gian cụ thể: ${formatTimestamp(computer.last_update)}`}>
                <Text>Lần cuối thấy: {getTimeSinceLastSeen()}</Text>
              </Tooltip>
            </Space>
          </Col>
          <Col xs={24} sm={12}>
            <Space align="center">
              <InfoCircleOutlined />
              <Tooltip title={computer.cpu_info || 'CPU không rõ'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.cpu_info || 'CPU không rõ'}
                 </Text>
              </Tooltip>
            </Space>
          </Col>
           <Col xs={24} sm={12}>
            <Space align="center">
              <LaptopOutlined />
               <Tooltip title={computer.os_info || 'OS không rõ'}>
                 <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                   {computer.os_info || 'OS không rõ'}
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
              <Tooltip title={computer.gpu_info || 'GPU không rõ'}>
                <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {computer.gpu_info || 'GPU không rõ'}
                </Text>
              </Tooltip>
            </Space>
          </Col>
          {computer.room?.name && (
            <Col xs={24} sm={12}>
              <Space align="center">
                <HomeOutlined />
                <Text>Phòng: {computer.room.name}</Text>
              </Space>
            </Col>
          )}
        </Row>

        <Divider style={{ margin: '20px 0 10px 0' }} />
        <Row align="middle" gutter={[8, 8]}>
          <Col span={24}>
            <Space>
              <InfoCircleOutlined />
              <Text type="secondary">Agent ID: {computer.unique_agent_id || 'Chưa đăng ký'}</Text>
            </Space>
          </Col>
        </Row>

        {computer.errors && Array.isArray(computer.errors) && computer.errors.length > 0 && (
          <>
            <Divider style={{ margin: '10px 0' }} />
            <div style={{ 
              backgroundColor: '#fff2f0', border: '1px solid #ffccc7', 
              padding: '10px', borderRadius: '4px', marginTop: '10px'
            }}>
              <Space align="start">
                <WarningOutlined style={{ color: '#ff4d4f', fontSize: '16px', marginTop: '3px' }} />
                <div>
                  <Text strong style={{ color: '#ff4d4f' }}>Phát hiện lỗi:</Text>
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
}); 

export default ComputerCard;
