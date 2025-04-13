import React, { useState, useEffect } from 'react';
import { Table, Button, Space, Popconfirm, message, Tag, Empty, Form, Input, Select, Row, Col, Card } from 'antd';
import { EditOutlined, DeleteOutlined, EyeOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import userService from '../../services/user.service';
import { LoadingComponent } from '../common';

const { Option } = Select;

const UserList = ({ onEdit, onView, onRefresh, refreshTrigger }) => {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  
  // Filter states
  const [username, setUsername] = useState('');
  const [role, setRole] = useState(null);
  const [isActive, setIsActive] = useState(null);
  const [form] = Form.useForm();

  useEffect(() => {
    fetchUsers();
  }, [refreshTrigger, pagination.current, pagination.pageSize]);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const response = await userService.getAllUsers(
        pagination.current, 
        pagination.pageSize, 
        username,
        role,
        isActive
      );
      
      // Parse response according to API.md structure
      let usersData = [];
      let totalUsers = 0;
      
      if (response?.data?.users && Array.isArray(response.data.users)) {
        // API spec: /api/users returns array in data.users
        usersData = response.data.users;
        totalUsers = response.data.total || usersData.length;
      } else if (response?.data && Array.isArray(response.data)) {
        // Alternative: array in data property
        usersData = response.data;
        totalUsers = usersData.length;
      } else if (response?.users && Array.isArray(response.users)) {
        // Alternative: direct users property
        usersData = response.users;
        totalUsers = response.total || usersData.length;
      } else if (Array.isArray(response)) {
        // Alternative: direct array
        usersData = response;
        totalUsers = usersData.length;
      }
      
      setUsers(usersData || []);
      setPagination(prev => ({ ...prev, total: totalUsers }));
    } catch (error) {
      message.error('Failed to fetch users');
      console.error('Error fetching users:', error);
      setUsers([]);
    } finally {
      setLoading(false);
    }
  };

  const handleTableChange = (pagination) => {
    setPagination(pagination);
  };

  const handleSearch = (values) => {
    setUsername(values.username || '');
    setRole(values.role || null);
    setIsActive(values.is_active !== undefined ? values.is_active : null);
    setPagination(prev => ({ ...prev, current: 1 })); // Reset to first page
    fetchUsers();
  };

  const handleReset = () => {
    form.resetFields();
    setUsername('');
    setRole(null);
    setIsActive(null);
    setPagination(prev => ({ ...prev, current: 1 }));
    fetchUsers();
  };

  const handleDelete = async (id) => {
    try {
      await userService.deleteUser(id);
      message.success('User deleted successfully');
      fetchUsers();
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error('Failed to delete user');
      console.error('Error deleting user:', error);
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
        // Handle both 'role' string and 'roles' array cases
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
        // Handle both is_active boolean and status string
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
      render: (_, record) => (
        <Space size="middle">
          <Button 
            type="default" 
            icon={<EyeOutlined />}
            onClick={() => onView(record.id)}
          >
            View
          </Button>
        </Space>
      ),
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
        <LoadingComponent type="section" tip="Đang tải danh sách người dùng..." />
      ) : (
        <Table
          columns={columns}
          dataSource={Array.isArray(users) ? users.map(user => ({ ...user, key: user.id })) : []}
          loading={false}
          pagination={pagination}
          onChange={handleTableChange}
          rowClassName="user-row"
          locale={{
            emptyText: <Empty description="No users found" />
          }}
        />
      )}
    </div>
  );
};

export default UserList;