import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Table, Button, Space, Popconfirm, message, Tag, Empty, Form, Input, Select, Row, Col, Card } from 'antd';
import { EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined, CheckCircleOutlined } from '@ant-design/icons';
import userService from '../../services/user.service';
import { LoadingComponent } from '../common';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';

const { Option } = Select;

const UserList = ({ onEdit, onRefresh, refreshTrigger }) => {
  const [form] = Form.useForm();

  // State for filters and pagination
  const [filters, setFilters] = useState({ username: '', role: null, is_active: null });
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [actionLoading, setActionLoading] = useState(false);

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

  const { data: usersResponse, loading, error: fetchError, refresh: fetchUsers, setData: setUsersData } = useSimpleFetch(
    fetchUsersCallback,
    [fetchUsersCallback],
    { errorMessage: 'Failed to fetch users' }
  );

  // Update pagination total when usersResponse changes
  useEffect(() => {
    if (usersResponse) {
      const totalUsers = usersResponse?.total || usersResponse?.data?.total || usersResponse?.data?.users?.length || usersResponse?.users?.length || (Array.isArray(usersResponse) ? usersResponse.length : 0);
      setPagination(prev => ({ ...prev, total: totalUsers }));
    }
  }, [usersResponse]);

  // Trigger refetch when external refreshTrigger changes
  useEffect(() => {
    if (refreshTrigger > 0) {
      fetchUsers();
    }
  }, [refreshTrigger, fetchUsers]);

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
    form.resetFields();
    setFilters({ username: '', role: null, is_active: null });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleDelete = async (id) => {
    setActionLoading(true);
    try {
      await userService.deleteUser(id);
      message.success('User deactivated successfully');
      fetchUsers();
      if (onRefresh) onRefresh();
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
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error('Failed to reactivate user');
      console.error('Error reactivating user:', error);
    } finally {
      setActionLoading(false);
    }
  };

  const columns = [
    {
      title: 'Username',
      dataIndex: 'username',
      key: 'username',
      sorter: (a, b) => a.username.localeCompare(b.username),
    },
    {
      title: 'Name',
      key: 'name',
      render: (_, record) => `${record.firstName || ''} ${record.lastName || ''}`.trim() || 'N/A',
    },
    {
      title: 'Email',
      dataIndex: 'email',
      key: 'email',
      render: email => email || 'N/A',
    },
    {
      title: 'Role',
      key: 'role',
      render: (_, record) => {
        if (record.roles && Array.isArray(record.roles)) {
          return (
            <>
              {record.roles.map(role => (
                <Tag color={role === 'admin' ? 'red' : role === 'moderator' ? 'orange' : 'green'} key={role}>
                  {role.toUpperCase()}
                </Tag>
              ))}
            </>
          );
        } else if (record.role) {
          return (
            <Tag color={record.role === 'admin' ? 'red' : record.role === 'moderator' ? 'orange' : 'green'}>
              {record.role.toUpperCase()}
            </Tag>
          );
        }
        return 'N/A';
      },
    },
    {
      title: 'Status',
      key: 'status',
      render: (_, record) => {
        const isActive = record.status === 'active' || record.is_active === true;
        const status = isActive ? 'active' : 'inactive';

        return (
          <span style={{ color: isActive ? 'green' : 'red' }}>
            {status.charAt(0).toUpperCase() + status.slice(1)}
          </span>
        );
      },
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => {
        const isActive = record.status === 'active' || record.is_active === true;
        return (
          <Space size="middle">
            <Button
              type="primary"
              icon={<EditOutlined />}
              onClick={() => onEdit(record)}
              disabled={actionLoading}
            >
              Edit
            </Button>
            {isActive ? (
              <Popconfirm
                title="Are you sure you want to deactivate this user?"
                onConfirm={() => handleDelete(record.id)}
                okText="Yes"
                cancelText="No"
                disabled={actionLoading}
              >
                <Button
                  type="default"
                  danger
                  icon={<DeleteOutlined />}
                  loading={actionLoading && record.id === users.find(u => u.id === record.id)?.id}
                  disabled={actionLoading}
                >
                  Deactivate
                </Button>
              </Popconfirm>
            ) : (
              <Popconfirm
                title="Are you sure you want to reactivate this user?"
                onConfirm={() => handleReactivate(record.id)}
                okText="Yes"
                cancelText="No"
                disabled={actionLoading}
              >
                <Button
                  type="primary"
                  icon={<CheckCircleOutlined />}
                  style={{ backgroundColor: '#52c41a', borderColor: '#52c41a' }}
                  loading={actionLoading && record.id === users.find(u => u.id === record.id)?.id}
                  disabled={actionLoading}
                >
                  Reactivate
                </Button>
              </Popconfirm>
            )}
          </Space>
        );
      },
    },
  ];

  return (
    <div className="user-list">
      <Card className="filter-card" style={{ marginBottom: '16px' }}>
        <Form
          form={form}
          name="user_filter"
          onFinish={handleSearch}
          layout="vertical"
          initialValues={{ username: '', role: null, is_active: null }}
        >
          <Row gutter={16}>
            <Col xs={24} sm={8}>
              <Form.Item name="username" label="Username">
                <Input placeholder="Search by username" prefix={<SearchOutlined />} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item name="role" label="Role">
                <Select placeholder="Select a role" allowClear>
                  <Option value="admin">Admin</Option>
                  <Option value="user">User</Option>
                </Select>
              </Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item name="is_active" label="Status">
                <Select placeholder="Select status" allowClear>
                  <Option value="true">Active</Option>
                  <Option value="false">Inactive</Option>
                </Select>
              </Form.Item>
            </Col>
          </Row>
          <Row>
            <Col span={24} style={{ textAlign: 'right' }}>
              <Space>
                <Button onClick={handleReset} icon={<ReloadOutlined />}>
                  Reset
                </Button>
                <Button type="primary" htmlType="submit" icon={<SearchOutlined />}>
                  Search
                </Button>
              </Space>
            </Col>
          </Row>
        </Form>
      </Card>

      {loading ? (
        <LoadingComponent type="section" tip="Loading user list..." />
      ) : fetchError ? (
        <Empty description={fetchError || "Failed to load users"} />
      ) : (
        <Table
          columns={columns}
          dataSource={Array.isArray(users) ? users.map(user => ({ ...user, key: user.id })) : []}
          loading={loading || actionLoading}
          pagination={pagination}
          onChange={handleTableChange}
          rowClassName="user-row"
          locale={{
            emptyText: <Empty description="No users found matching your criteria" />
          }}
        />
      )}
    </div>
  );
};

export default UserList;