/**
 * @fileoverview Room listing component with filtering and pagination
 * 
 * This component displays a list of rooms with filtering options,
 * pagination, and actions for viewing and editing rooms.
 * 
 * @module RoomList
 */
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Table, Button, Space, message, Empty, Tooltip, Form, Input, Select, Row, Col, Card } from 'antd';
import { EyeOutlined, EditOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';
import { useAuth } from '../../contexts/AuthContext';
import { LoadingComponent } from '../common';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';
import { useFormatting } from '../../hooks/useFormatting';

const { Option } = Select;

/**
 * RoomList Component
 * 
 * Displays a filterable, paginated table of rooms with actions for viewing and editing.
 * Admin users can also edit rooms. Includes a search form for filtering by name and assigned user.
 *
 * @component
 * @param {Object} props - Component props
 * @param {Function} props.onEdit - Callback when edit button is clicked with the room data
 * @param {Function} props.onView - Callback when view button is clicked with the room ID
 * @param {Function} props.onDelete - Callback when delete button is clicked with the room ID
 * @param {number|string} props.refreshTrigger - Value that changes to trigger a refresh of the room list
 * @returns {React.ReactElement} The rendered RoomList component
 */
const RoomList = ({ onEdit, onView, onDelete, refreshTrigger }) => {
  const { isAdmin } = useAuth();
  const { formatTimestamp } = useFormatting();
  const [form] = Form.useForm();

  // State for filters and pagination
  const [filters, setFilters] = useState({ name: '', assigned_user_id: null });
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });

  // Fetch users for filter dropdown (only for admin)
  const { data: usersData, loading: usersLoading } = useSimpleFetch(
    userService.getAllUsers,
    [isAdmin],
    { manual: !isAdmin, errorMessage: 'Failed to fetch users' }
  );
  const users = useMemo(() => usersData?.users || usersData?.data?.users || usersData?.data || usersData || [], [usersData]);

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

  const { data: roomsResponse, loading, error, refresh: fetchRooms } = useSimpleFetch(
    fetchRoomsCallback,
    [fetchRoomsCallback],
    { errorMessage: 'Failed to fetch rooms' }
  );

  // Update pagination total when roomsResponse changes
  useEffect(() => {
    if (roomsResponse) {
      const totalRooms = roomsResponse?.total || roomsResponse?.data?.total || roomsResponse?.data?.rooms?.length || roomsResponse?.rooms?.length || (Array.isArray(roomsResponse) ? roomsResponse.length : 0);
      setPagination(prev => ({ ...prev, total: totalRooms }));
    }
  }, [roomsResponse]);

  // Trigger refetch when external refreshTrigger changes
  useEffect(() => {
    if (refreshTrigger > 0) {
      fetchRooms();
    }
  }, [refreshTrigger, fetchRooms]);

  const rooms = useMemo(() => {
    return Array.isArray(roomsResponse) ? roomsResponse :
           roomsResponse?.data?.rooms || roomsResponse?.rooms || roomsResponse?.data || [];
  }, [roomsResponse]);

  /**
   * Handles pagination change
   * @function
   * @param {Object} newPagination - New pagination state
   */
  const handleTableChange = (newPagination) => {
    setPagination(prev => ({
      ...prev,
      current: newPagination.current,
      pageSize: newPagination.pageSize,
    }));
  };

  /**
   * Handles form search submission
   * @function
   * @param {Object} values - Form values with filter criteria
   */
  const handleSearch = (values) => {
    setFilters({
      name: values.name || '',
      assigned_user_id: values.assigned_user_id || null
    });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  /**
   * Resets all filters and refreshes the room list
   * @function
   */
  const handleReset = () => {
    form.resetFields();
    setFilters({ name: '', assigned_user_id: null });
    setPagination(prev => ({ ...prev, current: 1 }));
  };

  /**
   * Table column definitions with sorting, rendering, and actions
   */
  const columns = [
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
            <>
              <Button
                type="primary"
                icon={<EditOutlined />}
                onClick={() => onEdit(record)}
              >
                Edit
              </Button>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div className="room-list">
      <Card className="filter-card" style={{ marginBottom: '16px' }}>
        <Form
          form={form}
          name="room_filter"
          onFinish={handleSearch}
          layout="vertical"
          initialValues={{ name: '', assigned_user_id: null }}
        >
          <Row gutter={16}>
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
                    loading={usersLoading}
                    filterOption={(input, option) =>
                      option.children.toLowerCase().includes(input.toLowerCase())
                    }
                  >
                    {users.map(user => (
                      <Option key={user.id} value={user.id}>
                        {user.username} {user.firstName && user.lastName ? `(${user.firstName} ${user.lastName})` : ''}
                      </Option>
                    ))}
                  </Select>
                </Form.Item>
              </Col>
            )}
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
        <LoadingComponent type="section" tip="Loading room list..." />
      ) : error ? (
        <Empty description={error || "Failed to load rooms"} />
      ) : (
        <Table
          columns={columns}
          dataSource={Array.isArray(rooms) ? rooms.map(room => ({ ...room, key: room.id })) : []}
          loading={loading}
          pagination={pagination}
          onChange={handleTableChange}
          rowClassName="room-row"
          locale={{
            emptyText: <Empty description="No rooms found matching your criteria" />
          }}
        />
      )}
    </div>
  );
};

export default RoomList;