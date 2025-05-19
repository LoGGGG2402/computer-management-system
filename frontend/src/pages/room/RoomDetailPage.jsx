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
import React, { useState, useCallback } from 'react';
import { Card, Button, Space, message, Typography, Modal, Row, Col, Input, Divider, Select } from 'antd';
import { LayoutOutlined, ArrowLeftOutlined, EditOutlined, UserAddOutlined, SendOutlined, CodeOutlined, LoadingOutlined } from '@ant-design/icons';
import { useParams, useNavigate } from 'react-router-dom';
import RoomLayout from '../../components/room/RoomLayout';
import RoomForm from '../../components/room/RoomForm';
import AssignmentComponent from '../../components/admin/AssignmentComponent';
import roomService from '../../services/room.service';
import { useAuth } from '../../contexts/AuthContext';
import { useCommandHandle } from '../../contexts/CommandHandleContext';
import { LoadingComponent } from '../../components/common';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';
import { useModalState } from '../../hooks/useModalState';
import { useFormatting } from '../../hooks/useFormatting';

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
  const { isAdmin } = useAuth();
  const { sendCommand } = useCommandHandle();
  const { formatTimestamp } = useFormatting();

  const [command, setCommand] = useState('');
  const [commandType, setCommandType] = useState('console');
  const [commandLoading, setCommandLoading] = useState(false);
  const [assignedUsers, setAssignedUsers] = useState([]);

  // Memoize the fetch function
  const fetchRoomData = useCallback(async () => {
    if (!id) return null;
    const roomData = await roomService.getRoomById(id);
    // Assuming roomData includes assigned users or fetch separately if needed
    // If fetched separately, update assignedUsers state here
    if (roomData?.assigned_users) { // Check if assigned_users exists
        setAssignedUsers(roomData.assigned_users);
    }
    return roomData;
  }, [id]); // Dependency is id

  // Memoize the onError callback
  const handleFetchError = useCallback((err) => {
    message.error(`Failed to load room data: ${err.message}`);
  }, []); // No dependencies needed as message.error is stable

  const {
    data: room,
    loading,
    error,
    refresh: handleRefresh,
  } = useSimpleFetch(fetchRoomData, [id], {
    onError: handleFetchError, // Use the memoized callback
  });

  const computers = room?.computers || [];
  const editModal = useModalState('edit');
  const assignModal = useModalState('assign');

  const handleBack = () => {
    navigate('/rooms');
  };

  // Memoize success handlers if they cause re-renders unnecessarily,
  // though usually state updates within them are the intended trigger.
  const handleEditSuccess = useCallback((updatedRoom) => {
    editModal.closeModal();
    handleRefresh(); // Trigger refresh which will re-fetch data
    message.success('Room updated successfully');
  }, [editModal, handleRefresh]); // Dependencies: modal state and refresh function

  const handleAssignmentSuccess = useCallback((assignedUsersList) => {
    message.success('User assignments updated successfully');
    // The refresh should fetch the latest user list, but we can update optimistically
    setAssignedUsers(assignedUsersList || []);
    assignModal.closeModal();
    handleRefresh(); // Trigger refresh
  }, [assignModal, handleRefresh]); // Dependencies: modal state and refresh function


  const handleDeleteRoom = useCallback(async () => {
    Modal.confirm({
      title: 'Are you sure you want to delete this room?',
      content: 'This action cannot be undone.',
      okText: 'Yes, Delete',
      okType: 'danger',
      cancelText: 'No',
      onOk: async () => {
        try {
          if (!id) return; // Ensure id is defined
          await roomService.deleteRoom(id);
          message.success('Room deleted successfully');
          navigate('/rooms');
        } catch (error) {
          message.error('Failed to delete room');
          console.error('Error deleting room:', error);
        }
      },
    });
  }, [id, navigate]); // Dependencies: id and navigate

  const handleSendCommand = useCallback(async () => {
    if (!command.trim() || commandLoading) return;

    if (computers.length === 0) {
      message.warning('No computers available in this room');
      return;
    }

    setCommandLoading(true);

    try {
      // Use Promise.allSettled to get results for all promises, even if some fail
      const results = await Promise.allSettled(
        computers.map(computer =>
          sendCommand(computer.id, command.trim(), commandType)
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

      setCommand(''); // Clear command input on success or partial success
    } catch (error) {
      // This catch block might be less likely to be hit with Promise.allSettled,
      // unless there's an error *before* the promises start.
      message.error('An unexpected error occurred while initiating commands.');
      console.error('Error sending command to room:', error);
    } finally {
      setCommandLoading(false);
    }
  }, [command, commandType, commandLoading, computers, sendCommand]); // Dependencies

  const handleCommandTypeChange = (value) => {
    setCommandType(value);
  };

  // Effect to update assignedUsers when room data changes (if not handled in fetchRoomData)
  // This might be redundant if fetchRoomData already sets it.
  /*
  useEffect(() => {
      if (room?.assigned_users) {
          setAssignedUsers(room.assigned_users);
      }
  }, [room]); // Update when room data is fetched/updated
  */

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

  // --- Render Logic ---
  return (
    <div className="room-detail-page p-4 md:p-6"> {/* Added padding */}
      <Card
        title={
          <Space>
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleBack}
              aria-label="Back to Rooms list" // Accessibility
            >
              Back
            </Button>
            <Title level={4} style={{ margin: 0 }}>Room: {room.name}</Title> {/* Use Title for semantic heading */}
          </Space>
        }
        extra={
          <Space wrap> {/* Added wrap for smaller screens */}
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
              aria-label="Refresh room data" // Accessibility
            >
              Refresh
            </Button>
          </Space>
        }
        bordered={false} // Optional: remove card border for cleaner look
        className="shadow-md rounded-lg" // Add some shadow and rounding
      >
        {/* Room Details Section */}
        <div className="room-details mb-6 border-b pb-4"> {/* Added border bottom */}
          <Title level={5} className="mb-3">Details</Title> {/* Section heading */}
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} md={8}>
              <Text strong>ID:</Text> <Text copyable>{room.id}</Text> {/* Make ID copyable */}
            </Col>
            <Col xs={24} sm={12} md={8}>
              <Text strong>Name:</Text> {room.name}
            </Col>
             <Col xs={24} sm={12} md={8}>
              <Text strong>Created:</Text> {formatTimestamp(room.created_at)}
            </Col>
            <Col xs={24}>
              <Text strong>Description:</Text> {room.description || <Text type="secondary">N/A</Text>}
            </Col>
            <Col xs={24} sm={12} md={8}>
              <Text strong>Layout:</Text> {room.layout ? `${room.layout.rows} rows × ${room.layout.columns} columns` : <Text type="secondary">Not set</Text>}
            </Col>
            <Col xs={24} sm={12} md={8}>
              <Text strong>Capacity:</Text> {room.layout ? `${room.layout.rows * room.layout.columns} computers` : <Text type="secondary">N/A</Text>}
            </Col>
          </Row>

          {/* Display Assigned Users */}
          {assignedUsers.length > 0 && (
            <Row gutter={[16, 16]} className="mt-4">
              <Col span={24}>
                <Text strong>Assigned Users:</Text> {assignedUsers.map(user => user.username).join(', ')}
              </Col>
            </Row>
          )}
        </div>

        {/* Command Section */}
        <div className="command-section mb-6 border-b pb-4"> {/* Added border bottom */}
           <Divider orientation="left" plain> {/* Use AntD Divider */}
            <Space>
              <CodeOutlined />
              <Text strong>Room Command</Text>
            </Space>
          </Divider>

          <Row gutter={[16, 16]} align="middle" className="mb-3">
            {/* Command Type Selector */}
            <Col xs={24} sm={6} md={4}>
              <Select
                value={commandType}
                onChange={handleCommandTypeChange}
                style={{ width: '100%' }}
                disabled={commandLoading || computers.length === 0}
                aria-label="Select command type" // Accessibility
              >
                <Option value="console">Console</Option>
                <Option value="script">Script</Option>
                {/* Add other command types if applicable */}
              </Select>
            </Col>
            {/* Command Input */}
            <Col xs={24} sm={18} md={14}>
              <Input
                placeholder={`Enter ${commandType} command for ${computers.length} computer(s)...`}
                value={command}
                onChange={(e) => setCommand(e.target.value)}
                onPressEnter={handleSendCommand}
                prefix={<CodeOutlined className="site-form-item-icon" />} // Standard AntD class
                allowClear
                disabled={commandLoading || computers.length === 0}
                aria-label="Command input" // Accessibility
              />
            </Col>
            {/* Send Button */}
            <Col xs={24} md={6}>
              <Button
                type="primary"
                icon={commandLoading ? <LoadingOutlined /> : <SendOutlined />}
                onClick={handleSendCommand}
                disabled={!command.trim() || commandLoading || computers.length === 0}
                loading={commandLoading}
                block // Make button take full width on its column
                className="shadow-sm" // Add subtle shadow
              >
                {commandLoading ? 'Sending...' : `Send to ${computers.length} PC(s)`}
              </Button>
            </Col>
          </Row>

          {/* Computer Status Info */}
          <Row>
            <Col span={24}>
              {computers.length === 0 ? (
                <div className="text-center p-3 bg-gray-100 rounded border border-dashed"> {/* Enhanced styling */}
                  <Text type="secondary">No computers assigned to this room.</Text>
                   {isAdmin && (
                     <Text type="secondary" className="ml-2">
                       You can assign computers when editing the room or on the computer's detail page. {/* Helpful hint */}
                     </Text>
                   )}
                </div>
              ) : (
                <div className="p-3 bg-blue-50 rounded border border-blue-200 flex justify-between items-center"> {/* Use theme color */}
                  <Text>
                    <Text strong>{computers.length}</Text> computer(s) in this room.
                  </Text>
                  <Text type="secondary">
                    <Text strong className="text-green-600">{computers.filter(comp => comp.is_online).length}</Text> online. {/* Highlight online count */}
                  </Text>
                </div>
              )}
            </Col>
          </Row>
        </div>

        {/* Room Layout Section */}
        <div className="room-layout-section mt-6">
          <Divider orientation="left" plain> {/* Use AntD Divider */}
            <Space>
              <LayoutOutlined />
              <Text strong>Room Layout</Text>
            </Space>
          </Divider>
          {room.layout && room.layout.rows > 0 && room.layout.columns > 0 ? ( // Check for valid layout dimensions
            <RoomLayout
              room={room}
              computers={computers} // Pass computers data to layout
              // Add any other props needed by RoomLayout, e.g., onComputerClick
            />
          ) : (
            <div className="text-center p-6 border rounded bg-gray-50 border-dashed"> {/* Enhanced styling */}
              <Text type="secondary" className="block mb-2">Room layout not configured or invalid.</Text>
              {isAdmin && (
                <Button type="link" icon={<EditOutlined />} onClick={() => editModal.openModal('edit', room)}>
                  Configure Layout Now
                </Button>
              )}
            </div>
          )}
        </div>
      </Card>

      {/* Edit Room Modal */}
      <Modal
        title={<><EditOutlined /> Edit Room</>} // Add icon to title
        open={editModal.isModalVisible}
        onCancel={editModal.closeModal}
        footer={null} // Footer is likely handled within RoomForm
        width={600}
        destroyOnClose // Reset form state when modal closes
      >
        {editModal.selectedItem && ( // Ensure selectedItem exists before rendering form
            <RoomForm
              initialValues={editModal.selectedItem}
              onSuccess={handleEditSuccess}
              onCancel={editModal.closeModal}
            />
        )}
      </Modal>

      {/* Assign Users Modal */}
      <Modal
        title={<><UserAddOutlined /> Manage User Access</>} // Add icon to title
        open={assignModal.isModalVisible}
        onCancel={assignModal.closeModal}
        footer={null} // Footer is likely handled within AssignmentComponent
        width={800} // Consider adjusting width based on AssignmentComponent content
        destroyOnClose
      >
       {id && ( // Ensure id exists before rendering component
           <AssignmentComponent
             type="room"
             resourceId={id} // Pass id clearly as resourceId or similar prop
             resourceName={room?.name} // Pass room name for context
             onSuccess={handleAssignmentSuccess}
             onCancel={assignModal.closeModal}
           />
       )}
      </Modal>
    </div>
  );
};

export default RoomDetailPage;
