/**
 * @fileoverview Room management page for listing, creating, and modifying rooms
 * 
 * This component provides an interface for managing rooms in the system.
 * It includes functionality for viewing all rooms in a list format and managing them through
 * a modal form interface for both creating and editing rooms.
 * 
 * @module RoomsListPage
 */
import React, { useEffect } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { CommonList, CommonForm } from '../../components/common';
import { LoadingComponent } from '../../components/common';
import { useAppSelector, useAppDispatch, selectUserRole } from '../../app/index';
import {
  fetchRooms,
  fetchRoomById,
  createRoom,
  updateRoom,
  selectRooms,
  selectRoomLoading,
  selectRoomError,
  selectSelectedRoom,
  setRoomCurrentPage
} from '../../app/index';
import { useModalState } from '../../app/index';

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
  } = useModalState();

  const userRole = useAppSelector(selectUserRole);
  const isAdmin = userRole === 'admin';
  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  // Redux selectors
  const rooms = useAppSelector(selectRooms);
  const loading = useAppSelector(selectRoomLoading);
  const error = useAppSelector(selectRoomError);
  const selectedRoomData = useAppSelector(selectSelectedRoom);

  useEffect(() => {
    dispatch(fetchRooms());
  }, [dispatch]);

  /**
   * Handles opening the modal for creating a new room
   */
  const handleCreate = () => {
    openModal('create');
  };

  /**
   * Handles opening the modal for editing an existing room
   */
  const handleEdit = (room) => {
    openModal('edit', room);
    dispatch(fetchRoomById(room.id));
  };

  /**
   * Handles navigation to the room details page
   */
  const handleView = (roomId) => {
    navigate(`/rooms/${roomId}`, { state: { from: isAdmin ? 'admin' : 'user' } });
  };

  /**
   * Handles successful room creation or update
   */
  const handleSuccess = async (formData) => {
    try {
      if (modalAction === 'create') {
        await dispatch(createRoom(formData)).unwrap();
        message.success('Room created successfully');
      } else {
        await dispatch(updateRoom({ id: selectedRoom.id, ...formData })).unwrap();
        message.success('Room updated successfully');
      }
      closeModal();
      dispatch(fetchRooms());
    } catch (error) {
      message.error(`Failed to ${modalAction} room: ${error.message}`);
    }
  };

  /**
   * Handles canceling the room modal
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
        <CommonList 
          type="room"
          data={rooms}
          loading={loading}
          error={error}
          onEdit={handleEdit} 
          onView={handleView}
          onPageChange={(page) => dispatch(setRoomCurrentPage(page))}
        />
      </Card>

      <Modal
        title={modalTitle}
        open={isModalVisible}
        onCancel={handleCancel}
        footer={null}
        width={600}
      >
        {loading ? (
          <LoadingComponent type="section" tip="Loading room data..." />
        ) : (
          <CommonForm
            type="room"
            initialValues={selectedRoomData}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
          />
        )}
      </Modal>
    </div>
  );
};

export default RoomsListPage;