/**
 * @fileoverview User management page for listing, creating, and modifying users
 * 
 * This component provides an administrative interface for managing users in the system.
 * It includes functionality for viewing all users in a list format and managing them through
 * a modal form interface for both creating and editing users.
 * 
 * @module UsersListPage
 */
import React, { useState } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import UserList from '../../components/admin/UserList';
import UserForm from '../../components/admin/UserForm';
import { useModalState } from '../../hooks/useModalState';

const { Title } = Typography;

/**
 * Users List Page Component
 * 
 * Provides the main user management interface with:
 * - A table listing all users with search and filtering
 * - Create new user functionality
 * - Edit existing user functionality
 * 
 * @component
 * @returns {React.ReactElement} The rendered UsersListPage component
 */
const UsersListPage = () => {
  // Use the custom hook for modal state management
  const {
    isModalVisible,
    selectedItem: selectedUser,
    modalAction,
    openModal,
    closeModal,
  } = useModalState();

  const [refreshTrigger, setRefreshTrigger] = useState(0);

  /**
   * Handles opening the modal for creating a new user
   * @function
   */
  const handleCreate = () => {
    openModal('create');
  };

  /**
   * Handles opening the modal for editing an existing user
   * @function
   * @param {Object} user - The user object to edit
   */
  const handleEdit = (user) => {
    openModal('edit', user);
  };

  /**
   * Handles successful user creation or update
   * @function
   */
  const handleSuccess = () => {
    closeModal();
    setRefreshTrigger(prev => prev + 1);
    message.success(`User ${modalAction === 'create' ? 'created' : 'updated'} successfully`);
  };

  /**
   * Handles canceling the user modal
   * @function
   */
  const handleCancel = () => {
    closeModal();
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