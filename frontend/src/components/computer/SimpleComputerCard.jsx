import React, { useEffect } from 'react';
import { Card, Button, Popconfirm, message, Tooltip, Badge, Typography, Row, Col, Progress, Popover } from 'antd';
import { 
  DeleteOutlined, 
  DesktopOutlined, 
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  DatabaseOutlined,
  CodeOutlined, 
  HddOutlined,
  RocketOutlined
} from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext';
import { useSocket } from '../../contexts/SocketContext'; 
import { useCommandHandle } from '../../contexts/CommandHandleContext'; 
import computerService from '../../services/computer.service';

const { Text } = Typography;

export const cardStyle = {
  height: '180px',
  width: '100%', 
  overflow: 'hidden',
  borderRadius: '8px',
  boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
  display: 'flex',
  flexDirection: 'column'
};

const SimpleComputerCard = ({ computer, onView, onRefresh }) => {
  const { isAdmin } = useAuth();
  const { getComputerStatus, subscribeToComputer, unsubscribeFromComputer } = useSocket(); 
  const { commandResults, clearResult } = useCommandHandle(); 

  const computerId = computer?.id;

  useEffect(() => {
    if (computerId) {
      subscribeToComputer(computerId);
    }

    return () => {
      if (computerId) {
        unsubscribeFromComputer(computerId);
      }
    };
  }, [computerId, subscribeToComputer, unsubscribeFromComputer]);

  const statusData = getComputerStatus(computerId);
  const commandResult = computerId ? commandResults[computerId] : null;

  const handleDelete = async () => {
    if (!computerId) return;
    try {
      await computerService.deleteComputer(computerId);
      message.success('Computer deleted successfully');
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error('Failed to delete computer');
      console.error('Error deleting computer:', error);
    }
  };

  if (!computer) return null;

  const formatRAMSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };

  const formatDiskSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const gb = parseInt(bytes) / (1024 * 1024 * 1024);
    return `${gb.toFixed(2)} GB`;
  };
  
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

  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return 'Never';
    return getTimeAgo(computer.last_update);
  };

  const isOnline = statusData?.status === 'online' || 
    (computer.status === 'online') || 
    (computer.last_update && (new Date() - new Date(computer.last_update) < 5 * 60 * 1000)); 

  const cpuUsage = statusData?.cpuUsage ?? 0;
  const ramUsage = statusData?.ramUsage ?? 0;
  const diskUsage = statusData?.diskUsage ?? computer?.disk_usage ?? 0; 

  const getStatusColor = (value) => {
    if (value < 60) return '#52c41a'; 
    if (value < 80) return '#faad14'; 
    return '#f5222d'; 
  };

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
            onClick={() => clearResult(computerId)} 
          >
            Clear
          </Button>
        </div>
        
        <div style={{ marginBottom: '8px' }}>
          <Badge color={commandResult.exitCode === 0 ? 'green' : 'red'} />
          <Text style={{ marginLeft: '8px' }}>
            Exit Code: {commandResult.exitCode}
          </Text>
          <Text type="secondary" style={{ fontSize: '12px', marginLeft: '8px' }}>
            {getTimeAgo(commandResult.timestamp)}
          </Text>
        </div>
        
        {commandResult.stdout && (
          <div style={{ marginBottom: '8px' }}>
            <Text strong>Output:</Text>
            <div style={{ 
              background: '#f5f5f5', padding: '8px', borderRadius: '4px',
              maxHeight: '150px', overflowY: 'auto', whiteSpace: 'pre-wrap',
              wordBreak: 'break-all', fontSize: '12px'
            }}>
              {commandResult.stdout}
            </div>
          </div>
        )}
        
        {commandResult.stderr && (
          <div>
            <Text strong type="danger">Error:</Text>
            <div style={{ 
              background: '#fff2f0', padding: '8px', borderRadius: '4px',
              maxHeight: '150px', overflowY: 'auto', whiteSpace: 'pre-wrap',
              wordBreak: 'break-all', fontSize: '12px', color: '#ff4d4f'
            }}>
              {commandResult.stderr}
            </div>
          </div>
        )}
      </div>
    );
  };

  return (
    <Card
      hoverable
      size="small"
      style={{
        ...cardStyle,
        border: isOnline ? '1px solid #52c41a' : '1px solid #d9d9d9',
        height: '180px', 
        width: '100%',   
        maxWidth: '100%' 
      }}
      title={
        <div style={{ display: 'flex', alignItems: 'center', fontSize: '12px', width: '100%' }}>
          <Badge status={isOnline ? "success" : "default"} style={{ fontSize: '10px' }} />
          <span style={{ marginLeft: '4px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '12px', flex: 1 }}>
            {computer.name}
          </span>
          
          {commandResult && (
            <Popover
              content={renderCommandResultContent()}
              title={null}
              trigger="hover"
              placement="right"
              overlayStyle={{ width: '300px' }}
            >
              <Badge 
                count={<CodeOutlined style={{ color: commandResult.exitCode === 0 ? '#52c41a' : '#f5222d' }} />} 
                size="small"
                style={{ marginLeft: '4px' }}
              />
            </Popover>
          )}
        </div>
      }
      className="computer-card-simple" 
      extra={
        <div style={{ display: 'flex', flexShrink: 0 }}>
          <Tooltip title="View details">
            <Button 
              type="text" 
              icon={<DesktopOutlined style={{ fontSize: '12px' }} />} 
              size="small"
              onClick={() => onView(computerId)} 
            />
          </Tooltip>

          {isAdmin && (
            <Tooltip title="Delete computer">
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
      onClick={() => onView(computerId)}
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
            <Tooltip title="Last seen">
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
            <Tooltip title="Disk Space">
              <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                <HddOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                  {formatDiskSize(computer.total_disk_space)}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="GPU">
              <p style={{ margin: '2px 0', display: 'flex', alignItems: 'center', fontSize: '11px' }}>
                <RocketOutlined style={{ marginRight: '4px', fontSize: '11px' }} />
                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '11px' }}>
                  {computer.gpu_info || 'No GPU info'}
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
      
      <Row gutter={[4, 0]} style={{ marginTop: 'auto', paddingTop: '4px', width: '100%' }}> 
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
};

export default SimpleComputerCard;
