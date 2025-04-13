import React, { useState, useEffect, useRef } from 'react';
import { Card, Button, Space, message, Typography, Modal, Row, Col, Popconfirm, Input, Divider, Progress } from 'antd';
import { LayoutOutlined, ArrowLeftOutlined, EditOutlined, DeleteOutlined, UserAddOutlined, SendOutlined, CodeOutlined } from '@ant-design/icons';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import RoomLayout from '../../components/room/RoomLayout';
import RoomForm from '../../components/room/RoomForm';
import AssignmentComponent from '../../components/admin/AssignmentComponent';
import roomService from '../../services/room.service';
import { useAuth } from '../../contexts/AuthContext';
import { useCommandHandle } from '../../contexts/CommandHandleContext';
import { LoadingComponent } from '../../components/common';

const { Title } = Typography;

const RoomDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { isAdmin } = useAuth();
  const { sendCommand } = useCommandHandle();
  
  const [room, setRoom] = useState(null);
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  
  // Command input state
  const [command, setCommand] = useState('');
  
  // For the edit modal
  const [isEditModalVisible, setIsEditModalVisible] = useState(false);
  
  // For the user assignment modal
  const [isAssignModalVisible, setIsAssignModalVisible] = useState(false);
  // For users assigned to this room
  const [assignedUsers, setAssignedUsers] = useState([]);

  // Load room details and computers when component mounts or ID changes
  useEffect(() => {
    if (id) {
      fetchRoomData();
    }
  }, [id, refreshTrigger]);

  const fetchRoomData = async () => {
    try {
      setLoading(true);
      
      // Use the getRoomById method from roomService
      const roomData = await roomService.getRoomById(id);
      
      // Set the room data
      setRoom(roomData);
      
      // Set computers from the room response
      if (roomData && roomData.computers) {
        setComputers(roomData.computers);
      } else {
        setComputers([]);
      }

      // We're no longer fetching assigned users here since AssignmentComponent handles that
      // and will provide this data when needed
    } catch (error) {
      message.error('Failed to load room data');
      console.error('Error loading room data:', error);
    } finally {
      setLoading(false);
    }
  };
  
  const handleRefresh = () => {
    setRefreshTrigger(prev => prev + 1);
  };

  const handleBack = () => {
    // Determine if we came from admin or user page
    navigate('/rooms');
  };
  
  // Handle editing room
  const handleEditRoom = () => {
    setIsEditModalVisible(true);
  };
  
  const handleEditSuccess = () => {
    setIsEditModalVisible(false);
    handleRefresh();
    message.success('Room updated successfully');
  };
  
  const handleEditCancel = () => {
    setIsEditModalVisible(false);
  };
  
  // Handle user assignments
  const handleManageUsers = () => {
    setIsAssignModalVisible(true);
  };
  
  const handleAssignmentSuccess = (assignedUsersList) => {
    message.success('User assignments updated successfully');
    // Update the assigned users from the data passed from AssignmentComponent
    setAssignedUsers(assignedUsersList || []);
    setIsAssignModalVisible(false);
    handleRefresh(); // Also refresh the entire room data
  };
  
  const handleAssignmentCancel = () => {
    setIsAssignModalVisible(false);
  };
  
  // Handle room deletion
  const handleDeleteRoom = async () => {
    try {
      await roomService.deleteRoom(id);
      message.success('Room deleted successfully');
      // Navigate back to the rooms list
      navigate('/rooms');
    } catch (error) {
      message.error('Failed to delete room');
      console.error('Error deleting room:', error);
    }
  };

  // Simplified command sending without cooldown
  const handleSendCommand = async () => {
    if (!command.trim()) return;
    
    if (computers.length === 0) {
      message.warning('No computers available in this room');
      return;
    }
    
    try {
      // Use the sendCommand from useCommandHandle to send command to computers
      // We're only going to send the commands, we don't need to handle the responses
      const commandPromises = computers.map(computer => {
        return sendCommand(computer.id, command.trim())
          .catch(error => {
            console.warn(`Failed to send command to computer ${computer.id}:`, error);
            // We're ignoring individual errors as requested
            return null;
          });
      });
      
      // We don't need to wait for the results, but we'll wait for the commands to be sent
      // to ensure we have network connectivity before showing success message
      await Promise.all(commandPromises);
      
      // Show success message
      message.success(`Command sent to all computers in this room`);
      
      // Clear the command input after sending
      setCommand('');
    } catch (error) {
      message.error(error?.message || 'Failed to send command');
      console.error('Error sending command to room:', error);
    }
  };

  if (loading) {
    return <LoadingComponent type="section" tip="Loading room information..." />;
  }

  if (!room) {
    return (
      <Card className="room-detail-page">
        <div className="text-center p-8">
          <Title level={4}>Room not found or you don't have access to this room</Title>
          <Button 
            type="primary" 
            icon={<ArrowLeftOutlined />}
            onClick={handleBack}
            className="mt-4"
          >
            Back to Rooms
          </Button>
        </div>
      </Card>
    );
  }

  return (
    <div className="room-detail-page">
      <Card
        title={
          <Space>
            <Button 
              icon={<ArrowLeftOutlined />} 
              onClick={handleBack}
            >
              Back
            </Button>
            <span>Room: {room.name}</span>
          </Space>
        }
        extra={
          <Space>
            {isAdmin && (
              <Button 
                icon={<UserAddOutlined />}
                onClick={handleManageUsers}
              >
                Manage Users
              </Button>
            )}
            <Button 
              type="primary" 
              icon={<EditOutlined />}
              onClick={handleEditRoom}
            >
              Edit Room
            </Button>
            <Button 
              onClick={handleRefresh}
            >
              Refresh
            </Button>
          </Space>
        }
      >
        {/* Room details section */}
        <div className="room-details mb-6">
          <Row gutter={[16, 16]}>
            <Col span={12}>
              <p><strong>ID:</strong> {room.id}</p>
              <p><strong>Name:</strong> {room.name}</p>
              <p><strong>Description:</strong> {room.description || 'No description'}</p>
            </Col>
            <Col span={12}>
              <p><strong>Layout Dimensions:</strong> {room.layout ? `${room.layout.rows} rows × ${room.layout.columns} columns` : 'Not set'}</p>
              <p><strong>Total Capacity:</strong> {room.layout ? `${room.layout.rows * room.layout.columns} computers` : 'Not set'}</p>
              <p><strong>Created:</strong> {new Date(room.created_at).toLocaleString()}</p>
            </Col>
          </Row>

          {assignedUsers.length > 0 && (
            <Row gutter={[16, 16]}>
              <Col span={24}>
                <p><strong>Assigned Users:</strong> {assignedUsers.map(user => user.username).join(', ')}</p>
              </Col>
            </Row>
          )}
        </div>
        
        {/* Command section - Show for all authenticated users, backend will validate access */}
        <div className="command-section mb-6">
          <Divider>
            <Space>
              <CodeOutlined />
              <span>Room Command</span>
            </Space>
          </Divider>
          <Row gutter={[16, 16]} align="middle">
            <Col span={18}>
              <Input 
                placeholder="Enter command to send to all computers in this room..." 
                value={command}
                onChange={(e) => setCommand(e.target.value)}
                onPressEnter={handleSendCommand}
                prefix={<CodeOutlined />}
                allowClear
              />
            </Col>
            <Col span={6}>
              <Button 
                type="primary" 
                icon={<SendOutlined />} 
                onClick={handleSendCommand}
                disabled={!command.trim()}
                block
              >
                Send to Room
              </Button>
            </Col>
          </Row>
        </div>
        
        <div className="mt-6">
          <div className="room-layout-section">
            <div className="section-header mb-4">
              <Space>
                <LayoutOutlined />
                <Typography.Title level={4} style={{ margin: 0 }}>Room Layout</Typography.Title>
              </Space>
            </div>
            <RoomLayout 
              room={room}
              computers={computers}
              onRefresh={handleRefresh}
            />
          </div>
        </div>
      </Card>
      
      {/* Edit Room Modal */}
      <Modal
        title="Edit Room"
        open={isEditModalVisible}
        onCancel={handleEditCancel}
        footer={null}
        width={600}
      >
        <RoomForm
          initialValues={room}
          onSuccess={handleEditSuccess}
          onCancel={handleEditCancel}
        />
      </Modal>
      
      {/* User Assignment Modal */}
      <Modal
        title="Manage User Access"
        open={isAssignModalVisible}
        onCancel={handleAssignmentCancel}
        footer={null}
        width={800}
      >
        <AssignmentComponent 
          type="room" 
          id={id} 
          onSuccess={(users) => handleAssignmentSuccess(users)}
        />
      </Modal>
    </div>
  );
};

export default RoomDetailPage;