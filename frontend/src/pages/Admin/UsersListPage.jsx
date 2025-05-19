/**
 * @fileoverview User management page for listing, creating, and modifying users
 * 
 * This component provides an administrative interface for managing users in the system.
 * It includes functionality for viewing all users in a list format and managing them through
 * a modal form interface for both creating and editing users.
 * 
 * @module UsersListPage
 */
import React, { useState, useCallback, useMemo } from 'react';
import { Card, Button, Modal, Typography, message } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import DataList from '../../components/common/DataList';
import UserForm from '../../components/admin/UserForm';
import { useModalState } from '../../hooks/useModalState';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';
import userService from '../../services/user.service';

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
  // State for filters and pagination
  const [filters, setFilters] = useState({ username: '', role: null, is_active: null });
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [actionLoading, setActionLoading] = useState(false);

  // Use the custom hook for modal state management
  const {
    isModalVisible,
    selectedItem: selectedUser,
    modalAction,
    openModal,
    closeModal,
  } = useModalState();

  // Fetch users using useSimpleFetch
  const fetchUsersCallback = useCallback(async () => {
    const params = {
      page: pagination.current,
      limit: pagination.pageSize,
      ...(filters.username && { username: filters.username }),
      ...(filters.role && { role: filters.role }),
      ...(filters.is_active !== null && { is_active: filters.is_active }),
    };
    return await userService.getAllUsers(params);
  }, [pagination.current, pagination.pageSize, filters.username, filters.role, filters.is_active]);

  const { 
    data: usersResponse, 
    loading, 
    error: fetchError, 
    refresh: fetchUsers, 
    setData: setUsersData 
  } = useSimpleFetch(
    fetchUsersCallback,
    [fetchUsersCallback],
    { errorMessage: 'Failed to fetch users' }
  );

  // Update pagination total when usersResponse changes
  React.useEffect(() => {
    if (usersResponse) {
      const totalUsers = usersResponse?.total || usersResponse?.data?.total || usersResponse?.data?.users?.length || usersResponse?.users?.length || (Array.isArray(usersResponse) ? usersResponse.length : 0);
      setPagination(prev => ({ ...prev, total: totalUsers }));
    }
  }, [usersResponse]);

  const users = useMemo(() => {
    return Array.isArray(usersResponse) ? usersResponse :
      usersResponse?.data?.users || usersResponse?.users || usersResponse?.data || [];
  }, [usersResponse]);

  const handleTableChange = (newPagination) => {
    setPagination(prev => ({
      ...prev,
      current: newPagination.current,
      pageSize: newPagination.pageSize,
    }));
  };

  const handleSearch = (values) => {
    setFilters({
      username: values.username || '',
      role: values.role || null,
      is_active: values.is_active !== undefined ? values.is_active : null
    });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleReset = () => {
    setFilters({ username: '', role: null, is_active: null });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleDeactivate = async (id) => {
    setActionLoading(true);
    try {
      await userService.deactivateUser(id);
      message.success('User deactivated successfully');
      fetchUsers();
    } catch (error) {
      message.error('Failed to deactivate user');
      console.error('Error deactivating user:', error);
    } finally {
      setActionLoading(false);
    }
  };

  const handleReactivate = async (id) => {
    setActionLoading(true);
    try {
      await userService.reactivateUser(id);
      message.success('User reactivated successfully');
      fetchUsers();
    } catch (error) {
      message.error('Failed to reactivate user');
      console.error('Error reactivating user:', error);
    } finally {
      setActionLoading(false);
    }
  };

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
    fetchUsers();
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
        <DataList 
          type="user"
          data={users}
          loading={loading}
          error={fetchError}
          pagination={pagination}
          filters={filters}
          onTableChange={handleTableChange}
          onSearch={handleSearch}
          onReset={handleReset}
          onEdit={handleEdit}
          onDeactivate={handleDeactivate}
          onReactivate={handleReactivate}
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