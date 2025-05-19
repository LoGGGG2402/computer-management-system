/**
 * @fileoverview Room management page for listing, creating, and modifying rooms
 * 
 * This component provides an interface for managing rooms in the system.
 * It includes functionality for viewing all rooms in a list format and managing them through
 * a modal form interface for both creating and editing rooms.
 * 
 * @module RoomsListPage
 */
import React, { useState } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import RoomList from '../../components/room/RoomList';
import RoomForm from '../../components/room/RoomForm';
import { useAuth } from '../../contexts/AuthContext';
import { LoadingComponent } from '../../components/common';
import roomService from '../../services/room.service';
import { useModalState } from '../../hooks/useModalState';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';

const { Title } = Typography;

/**
 * Rooms List Page Component
 * 
 * Provides the main room management interface with:
 * - A table listing all rooms with search and filtering
 * - Create new room functionality (admin only)
 * - Edit existing room functionality (admin only)
 * - View room details functionality (all users)
 * 
 * Displays different UI based on user role (admin vs regular user)
 * 
 * @component
 * @returns {React.ReactElement} The rendered RoomsListPage component
 */
const RoomsListPage = () => {
  const {
    isModalVisible,
    selectedItem: selectedRoom,
    modalAction,
    openModal,
    closeModal,
    setSelectedItem,
  } = useModalState();

  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const { isAdmin } = useAuth();
  const navigate = useNavigate();

  const { loading: isModalLoading, executeFetch: fetchRoomDetailsForEdit } = useSimpleFetch(
    roomService.getRoomById,
    [],
    {
      manual: true,
      onSuccess: (roomData) => {
        setSelectedItem(roomData);
      },
      onError: (error) => {
        message.error('Failed to load room details');
        console.error('Error loading room details:', error);
        closeModal();
      },
    }
  );

  /**
   * Handles opening the modal for creating a new room
   * @function
   */
  const handleCreate = () => {
    openModal('create');
  };

  /**
   * Handles opening the modal for editing an existing room
   * Fetches detailed room data before opening the modal
   * 
   * @function
   * @param {Object} room - The room object to edit
   */
  const handleEdit = (room) => {
    openModal('edit', room);
    fetchRoomDetailsForEdit(room.id);
  };

  /**
   * Handles navigation to the room details page
   * 
   * @function
   * @param {number|string} roomId - ID of the room to view
   */
  const handleView = (roomId) => {
    navigate(`/rooms/${roomId}`, { state: { from: isAdmin ? 'admin' : 'user' } });
  };

  /**
   * Handles successful room creation or update
   * 
   * @function
   */
  const handleSuccess = () => {
    closeModal();
    setRefreshTrigger(prev => prev + 1);
    message.success(`Room ${modalAction === 'create' ? 'created' : 'updated'} successfully`);
  };

  /**
   * Handles canceling the room modal
   * 
   * @function
   */
  const handleCancel = () => {
    closeModal();
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