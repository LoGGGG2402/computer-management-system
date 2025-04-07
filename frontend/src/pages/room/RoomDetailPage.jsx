import React, { useState, useEffect } from 'react';
import { Card, Tabs, Button, Space, Skeleton, message, Typography, Modal, Row, Col, Popconfirm } from 'antd';
import { LayoutOutlined, UnorderedListOutlined, ArrowLeftOutlined, EditOutlined, DeleteOutlined, UserAddOutlined } from '@ant-design/icons';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import RoomLayout from '../../components/room/RoomLayout';
import RoomForm from '../../components/room/RoomForm';
import ComputerList from '../../components/computer/ComputerList';
import AssignmentComponent from '../../components/admin/AssignmentComponent';
import roomService from '../../services/room.service';
import { useAuth } from '../../contexts/AuthContext';

const { Title } = Typography;

const RoomDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { isAdmin, hasRoomAccess } = useAuth();
  
  const [room, setRoom] = useState(null);
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('layout');
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  
  // For the edit modal
  const [isEditModalVisible, setIsEditModalVisible] = useState(false);
  
  // For the user assignment modal
  const [isAssignModalVisible, setIsAssignModalVisible] = useState(false);

  // Load room details and computers when component mounts or ID changes
  useEffect(() => {
    if (id) {
      fetchRoomData();
    }
  }, [id, refreshTrigger]);

  const fetchRoomData = async () => {
    try {
      setLoading(true);
      // Get room details
      const roomResponse = await roomService.getRoomById(id);
      const roomData = roomResponse.data;
      setRoom(roomData);

      // Use computers already included in the room data
      if (roomData && roomData.computers) {
        setComputers(roomData.computers);
      } else {
        setComputers([]);
      }
    } catch (error) {
      message.error('Failed to load room data');
      console.error('Error loading room data:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleTabChange = (key) => {
    setActiveTab(key);
  };

  const handleEditComputer = (computer) => {
    // Navigate to computer edit page or open modal
    message.info('Edit computer functionality will be implemented soon');
  };

  const handleViewComputer = (computerId) => {
    // Navigate to computer detail page
    message.info('View computer functionality will be implemented soon');
  };
  
  const handleRefresh = () => {
    setRefreshTrigger(prev => prev + 1);
  };

  const handleBack = () => {
    // Determine if we came from admin or user page
    const isFromAdmin = location.state?.from === 'admin';
    navigate(isFromAdmin ? '/admin/rooms' : '/rooms');
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
  
  const handleAssignmentSuccess = () => {
    message.success('User assignments updated successfully');
    setIsAssignModalVisible(false);
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
      const isFromAdmin = location.state?.from === 'admin';
      navigate(isFromAdmin ? '/admin/rooms' : '/rooms');
    } catch (error) {
      message.error('Failed to delete room');
      console.error('Error deleting room:', error);
    }
  };

  // Check if user has access to this room
  const canAccessRoom = isAdmin || (room && hasRoomAccess(id));

  if (loading) {
    return (
      <Card className="room-detail-page">
        <Skeleton active/>
      </Card>
    );
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
            {canAccessRoom && (
              <Button 
                type="primary" 
                icon={<EditOutlined />}
                onClick={handleEditRoom}
              >
                Edit Room
              </Button>
            )}
            {isAdmin && (
              <Popconfirm
                title="Are you sure you want to delete this room?"
                onConfirm={handleDeleteRoom}
                okText="Yes"
                cancelText="No"
              >
                <Button 
                  danger
                  icon={<DeleteOutlined />}
                >
                  Delete
                </Button>
              </Popconfirm>
            )}
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
        </div>
        
        <div className="mt-6">
          <Tabs 
            activeKey={activeTab} 
            onChange={handleTabChange}
            type="card"
            tabBarStyle={{ marginBottom: 16 }}
            items={[
              {
                key: 'layout',
                label: (
                  <span>
                    <LayoutOutlined /> Room Layout
                  </span>
                ),
                children: (
                  <RoomLayout 
                    roomId={id}
                    computers={computers}
                    onEditComputer={handleEditComputer}
                    onViewComputer={handleViewComputer}
                    onRefresh={handleRefresh}
                  />
                )
              },
              {
                key: 'list',
                label: (
                  <span>
                    <UnorderedListOutlined /> Computer List
                  </span>
                ),
                children: (
                  <ComputerList 
                    computers={computers}
                    onEdit={handleEditComputer}
                    onView={handleViewComputer}
                    hideRoomColumn={true}
                    onRefresh={handleRefresh}
                  />
                )
              }
            ]}
          />
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
          onSuccess={handleAssignmentSuccess}
        />
      </Modal>
    </div>
  );
};

export default RoomDetailPage;