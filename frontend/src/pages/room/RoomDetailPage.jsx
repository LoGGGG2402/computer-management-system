/**
 * @fileoverview Room detail page for viewing and managing a specific room
 *
 * This component provides a detailed view of a room, including:
 * - Room information display
 * - Room computer layout visualization
 * - Room editing functionality (admin only)
 * - User assignment management (admin only)
 * - Command execution interface for all computers in the room
 *
 * @module RoomDetailPage
 */
import React, { useState, useEffect, useCallback } from 'react';
import { Card, Button, Space, message, Typography, Modal, Row, Col, Input, Select } from 'antd';
import { LayoutOutlined, ArrowLeftOutlined, EditOutlined, UserAddOutlined, SendOutlined, CodeOutlined, LoadingOutlined } from '@ant-design/icons';
import { useParams, useNavigate } from 'react-router-dom';
import RoomLayout from '../../components/room/RoomLayout';
import { CommonForm } from '../../components/common';
import AssignmentComponent from '../../components/admin/AssignmentComponent';
import { useAppSelector, useAppDispatch, selectUserRole } from '../../app/index';
import { LoadingComponent } from '../../components/common';
import { useModalState, useFormatting } from '../../app/index';
import {
  fetchRoomById,
  updateRoom,
  deleteRoom,
  selectSelectedRoom,
  selectRoomLoading,
  selectRoomError,
  selectRoomComputers,
  sendCommand
} from '../../app/index';

const { Title, Text } = Typography;
const { Option } = Select;

/**
 * Room Detail Page Component
 *
 * Shows detailed information about a specific room and provides functionality
 * to manage the room, including editing room details, managing user assignments,
 * viewing computers in the room, and sending commands to computers.
 *
 * @component
 * @returns {React.ReactElement} The rendered RoomDetailPage component
 */
const RoomDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const userRole = useAppSelector(selectUserRole);
  const isAdmin = userRole === 'admin';
  const dispatch = useAppDispatch();
  useFormatting();

  const [command, setCommand] = useState('');
  const [commandType, setCommandType] = useState('console');
  const [commandLoading, setCommandLoading] = useState(false);

  // Redux selectors
  const room = useAppSelector(selectSelectedRoom);
  const loading = useAppSelector(selectRoomLoading);
  const error = useAppSelector(selectRoomError);
  const computers = useAppSelector(selectRoomComputers);

  const editModal = useModalState('edit');
  const assignModal = useModalState('assign');

  useEffect(() => {
    if (id) {
      dispatch(fetchRoomById(id));
    }
  }, [dispatch, id]);

  const handleBack = () => {
    navigate('/rooms');
  };

  const handleRefresh = useCallback(() => {
    if (id) {
      dispatch(fetchRoomById(id));
    }
  }, [dispatch, id]);

  const handleEditSuccess = useCallback(async (formData) => {
    try {
      await dispatch(updateRoom({ id, ...formData })).unwrap();
      message.success('Room updated successfully');
      editModal.closeModal();
      handleRefresh();
    } catch (error) {
      message.error(`Failed to update room: ${error.message}`);
    }
  }, [dispatch, id, editModal, handleRefresh]);

  const handleAssignmentSuccess = useCallback(async (assignedUsersList) => {
    try {
      await dispatch(updateRoom({ id, assigned_users: assignedUsersList })).unwrap();
      message.success('User assignments updated successfully');
      assignModal.closeModal();
      handleRefresh();
    } catch (error) {
      message.error(`Failed to update user assignments: ${error.message}`);
    }
  }, [dispatch, id, assignModal, handleRefresh]);

  const handleDeleteRoom = useCallback(async () => {
    Modal.confirm({
      title: 'Are you sure you want to delete this room?',
      content: 'This action cannot be undone.',
      okText: 'Yes, Delete',
      okType: 'danger',
      cancelText: 'No',
      onOk: async () => {
        try {
          await dispatch(deleteRoom(id)).unwrap();
          message.success('Room deleted successfully');
          navigate('/rooms');
        } catch (error) {
          message.error(`Failed to delete room: ${error.message}`);
        }
      },
    });
  }, [dispatch, id, navigate]);

  const handleSendCommand = useCallback(async () => {
    if (!command.trim() || commandLoading) return;

    if (computers.length === 0) {
      message.warning('No computers available in this room');
      return;
    }

    setCommandLoading(true);

    try {
      const results = await Promise.allSettled(
        computers.map(computer =>
          dispatch(sendCommand({ computerId: computer.id, command: command.trim(), type: commandType }))
        )
      );

      const failedCommands = results.filter(result => result.status === 'rejected');
      const successfulCommands = results.filter(result => result.status === 'fulfilled');

      if (failedCommands.length > 0) {
        message.warning(`Command sent to ${successfulCommands.length} computer(s), but failed for ${failedCommands.length}. See console.`);
        console.error('Failed commands details:', failedCommands.map(f => f.reason));
      } else {
        message.success(`Command sent successfully to all ${computers.length} computers.`);
      }

      setCommand('');
    } catch (error) {
      message.error('An unexpected error occurred while initiating commands.');
      console.error('Error sending command to room:', error);
    } finally {
      setCommandLoading(false);
    }
  }, [command, commandType, commandLoading, computers, dispatch]);

  const handleCommandTypeChange = (value) => {
    setCommandType(value);
  };

  if (loading && !room) {
    return <LoadingComponent type="section" tip="Loading room information..." />;
  }

  if (error || !room) {
    return (
      <Card className="room-detail-page">
        <div className="text-center p-8">
          <Title level={4}>{error ? 'Error loading room' : 'Room not found'}</Title>
          {error && <Text type="danger" className="block mb-4">{error}</Text>}
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
    <div className="room-detail-page p-4 md:p-6">
      <Card
        title={
          <Space>
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleBack}
              aria-label="Back to Rooms list"
            >
              Back
            </Button>
            <Title level={4} style={{ margin: 0 }}>Room: {room.name}</Title>
          </Space>
        }
        extra={
          <Space wrap>
            {isAdmin && (
              <>
                <Button
                  icon={<UserAddOutlined />}
                  onClick={() => assignModal.openModal('assign')}
                >
                  Manage Users
                </Button>
                <Button
                  type="primary"
                  icon={<EditOutlined />}
                  onClick={() => editModal.openModal('edit', room)}
                >
                  Edit Room
                </Button>
                <Button
                  danger
                  onClick={handleDeleteRoom}
                >
                  Delete Room
                </Button>
              </>
            )}
            <Button
              onClick={handleRefresh}
              loading={loading}
              aria-label="Refresh room data"
            >
              Refresh
            </Button>
          </Space>
        }
      >
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={16}>
            <Card title={<Space><LayoutOutlined /> Room Layout</Space>}>
              <RoomLayout computers={computers} />
            </Card>
          </Col>
          <Col xs={24} lg={8}>
            <Card title={<Space><CodeOutlined /> Command Console</Space>}>
              <Space direction="vertical" style={{ width: '100%' }}>
                <Select
                  value={commandType}
                  onChange={handleCommandTypeChange}
                  style={{ width: '100%' }}
                >
                  <Option value="console">Console Command</Option>
                  <Option value="power">Power Command</Option>
                </Select>
                <Input.TextArea
                  value={command}
                  onChange={(e) => setCommand(e.target.value)}
                  placeholder="Enter command..."
                  autoSize={{ minRows: 3, maxRows: 6 }}
                />
                <Button
                  type="primary"
                  icon={commandLoading ? <LoadingOutlined /> : <SendOutlined />}
                  onClick={handleSendCommand}
                  loading={commandLoading}
                  block
                >
                  Send to All Computers
                </Button>
              </Space>
            </Card>
          </Col>
        </Row>
      </Card>

      <Modal
        title="Edit Room"
        open={editModal.isModalVisible}
        onCancel={editModal.closeModal}
        footer={null}
        width={600}
      >
        <CommonForm
          type="room"
          initialValues={room}
          onSuccess={handleEditSuccess}
          onCancel={editModal.closeModal}
        />
      </Modal>

      <Modal
        title="Manage Users"
        open={assignModal.isModalVisible}
        onCancel={assignModal.closeModal}
        footer={null}
        width={600}
      >
        <AssignmentComponent
          roomId={id}
          initialAssignments={room.assigned_users}
          onSuccess={handleAssignmentSuccess}
          onCancel={assignModal.closeModal}
        />
      </Modal>
    </div>
  );
};

export default RoomDetailPage;
