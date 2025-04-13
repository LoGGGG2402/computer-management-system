import React, { useState, useEffect } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import RoomList from '../../components/room/RoomList';
import RoomForm from '../../components/room/RoomForm';
import { useAuth } from '../../contexts/AuthContext';
import { LoadingComponent } from '../../components/common';
import roomService from '../../services/room.service';

const { Title } = Typography;

const RoomsListPage = () => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedRoom, setSelectedRoom] = useState(null);
  const [modalAction, setModalAction] = useState('create'); // 'create' or 'edit'
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const [isModalLoading, setIsModalLoading] = useState(false);
  const { isAdmin } = useAuth();
  const navigate = useNavigate();

  const handleCreate = () => {
    setSelectedRoom(null);
    setModalAction('create');
    setIsModalVisible(true);
  };

  const handleEdit = (room) => {
    // Set loading state while getting room details
    setIsModalLoading(true);
    setSelectedRoom(room);
    setModalAction('edit');
    setIsModalVisible(true);
    
    // If we need detailed room data, fetch it
    roomService.getRoomById(room.id)
      .then(roomData => {
        setSelectedRoom(roomData);
        setIsModalLoading(false);
      })
      .catch(error => {
        message.error('Failed to load room details');
        console.error('Error loading room details:', error);
        setIsModalLoading(false);
      });
  };

  const handleView = (roomId) => {
    // Navigate to the RoomDetailPage with state indicating where we came from
    navigate(`/rooms/${roomId}`, { state: { from: isAdmin ? 'admin' : 'user' } });
  };

  const handleSuccess = () => {
    setIsModalVisible(false);
    // Trigger room list refresh
    setRefreshTrigger(prev => prev + 1);
    message.success(`Room ${modalAction === 'create' ? 'created' : 'updated'} successfully`);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  // handleDelete function removed - room deletion not allowed

  const modalTitle = modalAction === 'create' ? 'Create New Room' : 'Edit Room';

  return (
    <div className="room-page">
      <Card
        title={<Title level={3}>{isAdmin ? 'Room Management' : 'Available Rooms'}</Title>}
        extra={
          isAdmin && (
            <Button 
              type="primary" 
              icon={<PlusOutlined />} 
              onClick={handleCreate}
            >
              Add New Room
            </Button>
          )
        }
      >
        <RoomList 
          onEdit={handleEdit} 
          onView={handleView}
          refreshTrigger={refreshTrigger} 
        />
      </Card>

      <Modal
        title={modalTitle}
        open={isModalVisible}
        onCancel={handleCancel}
        footer={null}
        width={600}
      >
        {isModalLoading ? (
          <LoadingComponent type="section" tip="Loading room data..." />
        ) : (
          <RoomForm
            initialValues={selectedRoom}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
          />
        )}
      </Modal>
    </div>
  );
};

export default RoomsListPage;