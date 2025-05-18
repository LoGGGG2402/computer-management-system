import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Table, Button, Space, Popconfirm, message, Tag, Empty, Form, Input, Select, Row, Col, Card, Tooltip } from 'antd';
import { EditOutlined, DeleteOutlined, SearchOutlined, ReloadOutlined, CheckCircleOutlined, EyeOutlined } from '@ant-design/icons';
import { LoadingComponent } from './index';
import { useAppSelector, selectUserRole, useReduxFetch, useFormatting } from '../../app/index';

const { Option } = Select;

const CommonList = ({ 
  type, 
  onEdit, 
  onView, 
  onRefresh,
  refreshTrigger,
  service,
  secondaryService,
}) => {
  const userRole = useAppSelector(selectUserRole);
  const isAdmin = userRole === 'admin';
  const [form] = Form.useForm();
  const { formatTimestamp } = useFormatting();
  // State for filters and pagination
  const [filters, setFilters] = useState(
    type === 'user' 
      ? { username: '', role: null, is_active: null }
      : { name: '', assigned_user_id: null }
  );
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [actionLoading, setActionLoading] = useState(false);

  // Fetch secondary data (users for room list)
  const { data: secondaryData, loading: secondaryLoading } = useReduxFetch(
    secondaryService?.getAllUsers,
    [isAdmin, type],
    { manual: !isAdmin || type !== 'room', errorMessage: 'Failed to fetch secondary data' }
  );

  const secondaryItems = useMemo(() => 
    secondaryData?.users || secondaryData?.data?.users || secondaryData?.data || secondaryData || [], 
    [secondaryData]
  );

  // Fetch main data using useReduxFetch
  const fetchDataCallback = useCallback(async () => {
    const params = {
      page: pagination.current,
      limit: pagination.pageSize,
      ...filters
    };
    return await service.getAllItems(params);
  }, [pagination, filters, service]);

  const { data: response, loading, error, refresh: fetchData } = useReduxFetch(
    fetchDataCallback,
    [fetchDataCallback],
    { errorMessage: `Failed to fetch ${type}s` }
  );

  // Update pagination total when response changes
  useEffect(() => {
    if (response) {
      const total = response?.total || response?.data?.total || 
                   response?.data?.items?.length || response?.items?.length || 
                   (Array.isArray(response) ? response.length : 0);
      setPagination(prev => ({ ...prev, total }));
    }
  }, [response]);

  // Trigger refetch when external refreshTrigger changes
  useEffect(() => {
    if (refreshTrigger > 0) {
      fetchData();
    }
  }, [refreshTrigger, fetchData]);

  const items = useMemo(() => {
    return Array.isArray(response) ? response :
           response?.data?.items || response?.items || response?.data || [];
  }, [response]);

  const handleTableChange = (newPagination) => {
    setPagination(prev => ({
      ...prev,
      current: newPagination.current,
      pageSize: newPagination.pageSize,
    }));
  };

  const handleSearch = (values) => {
    if (type === 'user') {
      setFilters({
        username: values.username || '',
        role: values.role || null,
        is_active: values.is_active !== undefined ? values.is_active : null
      });
    } else {
      setFilters({
        name: values.name || '',
        assigned_user_id: values.assigned_user_id || null
      });
    }
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleReset = () => {
    form.resetFields();
    setFilters(type === 'user' 
      ? { username: '', role: null, is_active: null }
      : { name: '', assigned_user_id: null }
    );
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  const handleDelete = async (id) => {
    setActionLoading(true);
    try {
      await service.deleteItem(id);
      message.success(`${type === 'user' ? 'User' : 'Room'} deactivated successfully`);
      fetchData();
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error(`Failed to deactivate ${type}`);
      console.error(`Error deactivating ${type}:`, error);
    } finally {
      setActionLoading(false);
    }
  };

  const handleReactivate = async (id) => {
    setActionLoading(true);
    try {
      await service.reactivateItem(id);
      message.success(`${type === 'user' ? 'User' : 'Room'} reactivated successfully`);
      fetchData();
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error(`Failed to reactivate ${type}`);
      console.error(`Error reactivating ${type}:`, error);
    } finally {
      setActionLoading(false);
    }
  };

  const renderUserColumns = () => [
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
                title={`Are you sure you want to deactivate this ${type}?`}
                onConfirm={() => handleDelete(record.id)}
                okText="Yes"
                cancelText="No"
                disabled={actionLoading}
              >
                <Button
                  type="default"
                  danger
                  icon={<DeleteOutlined />}
                  loading={actionLoading && record.id === items.find(i => i.id === record.id)?.id}
                  disabled={actionLoading}
                >
                  Deactivate
                </Button>
              </Popconfirm>
            ) : (
              <Popconfirm
                title={`Are you sure you want to reactivate this ${type}?`}
                onConfirm={() => handleReactivate(record.id)}
                okText="Yes"
                cancelText="No"
                disabled={actionLoading}
              >
                <Button
                  type="primary"
                  icon={<CheckCircleOutlined />}
                  style={{ backgroundColor: '#52c41a', borderColor: '#52c41a' }}
                  loading={actionLoading && record.id === items.find(i => i.id === record.id)?.id}
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

  const renderRoomColumns = () => [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (text) => (
        <Tooltip title={text}>
          <span>{text}</span>
        </Tooltip>
      ),
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
      render: (text) => (
        <Tooltip title={text || 'No description'}>
          <span>{text || 'No description'}</span>
        </Tooltip>
      ),
    },
    {
      title: 'Layouts',
      key: 'layout',
      render: (_, record) => (
        <Tooltip title={record.layout ? `Rows: ${record.layout.rows}, Columns: ${record.layout.columns}` : 'Not set'}>
          <span>{record.layout ? `${record.layout.rows}×${record.layout.columns}` : 'N/A'}</span>
        </Tooltip>
      ),
    },
    {
      title: 'Created',
      dataIndex: 'created_at',
      key: 'created_at',
      render: (date) => formatTimestamp(date).split(' ')[0],
      sorter: (a, b) => new Date(a.created_at) - new Date(b.created_at),
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
          {isAdmin && (
            <Button
              type="primary"
              icon={<EditOutlined />}
              onClick={() => onEdit(record)}
            >
              Edit
            </Button>
          )}
        </Space>
      ),
    },
  ];

  const renderUserFilter = () => (
    <>
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
    </>
  );

  const renderRoomFilter = () => (
    <>
      <Col xs={24} sm={12}>
        <Form.Item name="name" label="Room Name">
          <Input placeholder="Search by room name" prefix={<SearchOutlined />} />
        </Form.Item>
      </Col>
      {isAdmin && (
        <Col xs={24} sm={12}>
          <Form.Item name="assigned_user_id" label="Assigned User">
            <Select
              placeholder="Filter by assigned user"
              allowClear
              showSearch
              optionFilterProp="children"
              loading={secondaryLoading}
              filterOption={(input, option) =>
                option.children.toLowerCase().includes(input.toLowerCase())
              }
            >
              {secondaryItems.map(user => (
                <Option key={user.id} value={user.id}>
                  {user.username} {user.firstName && user.lastName ? `(${user.firstName} ${user.lastName})` : ''}
                </Option>
              ))}
            </Select>
          </Form.Item>
        </Col>
      )}
    </>
  );

  return (
    <div className={`${type}-list`}>
      <Card className="filter-card" style={{ marginBottom: '16px' }}>
        <Form
          form={form}
          name={`${type}_filter`}
          onFinish={handleSearch}
          layout="vertical"
          initialValues={type === 'user' 
            ? { username: '', role: null, is_active: null }
            : { name: '', assigned_user_id: null }
          }
        >
          <Row gutter={16}>
            {type === 'user' ? renderUserFilter() : renderRoomFilter()}
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
        <LoadingComponent type="section" tip={`Loading ${type} list...`} />
      ) : error ? (
        <Empty description={error || `Failed to load ${type}s`} />
      ) : (
        <Table
          columns={type === 'user' ? renderUserColumns() : renderRoomColumns()}
          dataSource={Array.isArray(items) ? items.map(item => ({ ...item, key: item.id })) : []}
          loading={loading || actionLoading}
          pagination={pagination}
          onChange={handleTableChange}
          rowClassName={`${type}-row`}
          locale={{
            emptyText: <Empty description={`No ${type}s found matching your criteria`} />
          }}
        />
      )}
    </div>
  );
};

export default CommonList;