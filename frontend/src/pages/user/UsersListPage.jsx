import React, { useState } from 'react';
import { Card, Button, Modal, Tabs, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import UserList from '../../components/admin/UserList';
import UserForm from '../../components/admin/UserForm';
import AssignmentComponent from '../../components/admin/AssignmentComponent';

const { Title } = Typography;

const UsersListPage = () => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [modalAction, setModalAction] = useState('create'); // 'create', 'edit', 'view'
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const [selectedUserId, setSelectedUserId] = useState(null);

  const handleCreate = () => {
    setSelectedUser(null);
    setModalAction('create');
    setIsModalVisible(true);
  };

  const handleEdit = (user) => {
    setSelectedUser(user);
    setModalAction('edit');
    setIsModalVisible(true);
  };

  const handleView = (userId) => {
    setSelectedUserId(userId);
    setModalAction('view');
    setIsModalVisible(true);
  };

  const handleSuccess = () => {
    setIsModalVisible(false);
    // Trigger user list refresh
    setRefreshTrigger(prev => prev + 1);
    message.success(`User ${modalAction === 'create' ? 'created' : 'updated'} successfully`);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  const modalTitle = 
    modalAction === 'create' ? 'Create New User' : 
    modalAction === 'edit' ? 'Edit User' : 
    'User Details';

  // Define the tab items for the Tabs component using the new format
  const tabItems = [
    {
      key: 'details',
      label: 'Details',
      children: <div>User details will be displayed here</div>
    },
    {
      key: 'rooms',
      label: 'Room Assignments',
      children: (
        <AssignmentComponent 
          type="user" 
          id={selectedUserId} 
          onSuccess={() => message.success('Room assignments updated')}
        />
      )
    }
  ];

  return (
    <div className="user-management-page">
      <Card
        title={<Title level={3}>User Management</Title>}
        extra={
          <Button 
            type="primary" 
            icon={<PlusOutlined />} 
            onClick={handleCreate}
          >
            Add New User
          </Button>
        }
      >
        <UserList 
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
        width={modalAction === 'view' ? 800 : 600}
      >
        {modalAction === 'view' ? (
          <Tabs defaultActiveKey="details" items={tabItems} />
        ) : (
          <UserForm
            initialValues={selectedUser}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
          />
        )}
      </Modal>
    </div>
  );
};

export default UsersListPage;