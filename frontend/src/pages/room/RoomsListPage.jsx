/**
 * @fileoverview Room management page for listing, creating, and modifying rooms
 * 
 * This component provides an interface for managing rooms in the system.
 * It includes functionality for viewing all rooms in a list format and managing them through
 * a modal form interface for both creating and editing rooms.
 * 
 * @module RoomsListPage
 */
import React, { useState, useCallback, useMemo } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import DataList from '../../components/common/DataList';
import RoomForm from '../../components/room/RoomForm';
import { useAuth } from '../../contexts/AuthContext';
import { Loading } from '../../components/common';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';
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
  const { isAdmin } = useAuth();
  const navigate = useNavigate();

  // State for filters and pagination
  const [filters, setFilters] = useState({ name: '', assigned_user_id: null });
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });

  const {
    isModalVisible,
    selectedItem: selectedRoom,
    modalAction,
    openModal,
    closeModal,
    setSelectedItem,
  } = useModalState();

  // Fetch users for filter dropdown (only for admin)
  const { data: usersData, loading: usersLoading } = useSimpleFetch(
    userService.getAllUsers,
    [isAdmin],
    { manual: !isAdmin, errorMessage: 'Failed to fetch users' }
  );

  const users = useMemo(() => 
    usersData?.users || usersData?.data?.users || usersData?.data || usersData || [], 
    [usersData]
  );

  // Fetch rooms using useSimpleFetch
  const fetchRoomsCallback = useCallback(async () => {
    const params = {
      page: pagination.current,
      limit: pagination.pageSize,
      ...(filters.name && { name: filters.name }),
      ...(filters.assigned_user_id && { assigned_user_id: filters.assigned_user_id }),
    };
    return await roomService.getAllRooms(params);
  }, [pagination.current, pagination.pageSize, filters.name, filters.assigned_user_id]);

  const { 
    data: roomsResponse, 
    loading: roomsLoading, 
    error: roomsError, 
    refresh: fetchRooms 
  } = useSimpleFetch(
    fetchRoomsCallback,
    [fetchRoomsCallback],
    { errorMessage: 'Failed to fetch rooms' }
  );

  // Update pagination total when roomsResponse changes
  React.useEffect(() => {
    if (roomsResponse) {
      const totalRooms = roomsResponse?.total || roomsResponse?.data?.total || roomsResponse?.data?.rooms?.length || roomsResponse?.rooms?.length || (Array.isArray(roomsResponse) ? roomsResponse.length : 0);
      setPagination(prev => ({ ...prev, total: totalRooms }));
    }
  }, [roomsResponse]);

  const rooms = useMemo(() => {
    return Array.isArray(roomsResponse) ? roomsResponse :
           roomsResponse?.data?.rooms || roomsResponse?.rooms || roomsResponse?.data || [];
  }, [roomsResponse]);

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

  const handleTableChange = (newPagination) => {
    setPagination(prev => ({
      ...prev,
      current: newPagination.current,
      pageSize: newPagination.pageSize,
    }));
  };

  const handleSearch = (values) => {
    setFilters({
      name: values.name || '',
      assigned_user_id: values.assigned_user_id || null
    });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleReset = () => {
    setFilters({ name: '', assigned_user_id: null });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

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
    fetchRooms();
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
        <DataList 
          type="room"
          data={rooms}
          loading={roomsLoading}
          error={roomsError}
          pagination={pagination}
          filters={filters}
          filterOptions={users}
          filterOptionsLoading={usersLoading}
          onTableChange={handleTableChange}
          onSearch={handleSearch}
          onReset={handleReset}
          onEdit={handleEdit}
          onView={handleView}
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
          <Loading type="section" tip="Loading room data..." />
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