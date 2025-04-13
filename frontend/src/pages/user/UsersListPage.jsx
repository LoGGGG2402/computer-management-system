import React, { useState } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import UserList from '../../components/admin/UserList';
import UserForm from '../../components/admin/UserForm';

const { Title } = Typography;

const UsersListPage = () => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [modalAction, setModalAction] = useState('create'); // 'create', 'edit'
  const [refreshTrigger, setRefreshTrigger] = useState(0);

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

  const handleSuccess = () => {
    setIsModalVisible(false);
    // Trigger user list refresh
    setRefreshTrigger(prev => prev + 1);
    message.success(`User ${modalAction === 'create' ? 'created' : 'updated'} successfully`);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  const modalTitle = modalAction === 'create' ? 'Create New User' : 'Edit User';

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
        <UserForm
          initialValues={selectedUser}
          onSuccess={handleSuccess}
          onCancel={handleCancel}
        />
      </Modal>
    </div>
  );
};

export default UsersListPage;