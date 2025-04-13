import React, { useState } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import RoomList from '../../components/room/RoomList';
import RoomForm from '../../components/room/RoomForm';
import { useAuth } from '../../contexts/AuthContext';

const { Title } = Typography;

const RoomsListPage = () => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedRoom, setSelectedRoom] = useState(null);
  const [modalAction, setModalAction] = useState('create'); // 'create' or 'edit'
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const { isAdmin, hasRoomAccess } = useAuth();
  const navigate = useNavigate();

  const handleCreate = () => {
    setSelectedRoom(null);
    setModalAction('create');
    setIsModalVisible(true);
  };

  const handleEdit = (room) => {
    // Check if user has access to edit this room (only needed for non-admin view)
    if (isAdmin || hasRoomAccess(room.id)) {
      setSelectedRoom(room);
      setModalAction('edit');
      setIsModalVisible(true);
    } else {
      message.error('You do not have permission to edit this room');
    }
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
          onRefresh={() => setRefreshTrigger(prev => prev + 1)} 
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
        <RoomForm
          initialValues={selectedRoom}
          onSuccess={handleSuccess}
          onCancel={handleCancel}
        />
      </Modal>
    </div>
  );
};

export default RoomsListPage;