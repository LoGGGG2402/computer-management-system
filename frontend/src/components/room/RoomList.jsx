import React, { useState, useEffect } from 'react';
import { Table, Button, Space, message, Empty, Tooltip, Form, Input, Select, Row, Col, Card } from 'antd';
import { EyeOutlined, EditOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';
import { useAuth } from '../../contexts/AuthContext';
import { LoadingComponent } from '../common';

const { Option } = Select;

const RoomList = ({ onEdit, onView, onDelete, refreshTrigger }) => {
  const [rooms, setRooms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const { isAdmin } = useAuth();
  
  // Filter states
  const [name, setName] = useState('');
  const [assignedUserId, setAssignedUserId] = useState(null);
  const [users, setUsers] = useState([]);
  const [form] = Form.useForm();

  useEffect(() => {
    fetchRooms();
    if (isAdmin) {
      fetchUsers();
    }
  }, [refreshTrigger, pagination.current, pagination.pageSize]);

  const fetchUsers = async () => {
    try {
      const response = await userService.getAllUsers();
      setUsers(response?.data?.users || response?.users || response?.data || response || []);
    } catch (error) {
      console.error('Error fetching users:', error);
    }
  };

  const fetchRooms = async () => {
    try {
      setLoading(true);
      // Create filters object according to the expected API
      const filters = {
        page: pagination.current,
        limit: pagination.pageSize
      };

      if (name) filters.name = name;
      if (assignedUserId) filters.assigned_user_id = assignedUserId;

      const response = await roomService.getAllRooms(filters);
      
      // Simplified response handling, expecting the room service to handle format conversions
      const roomsData = Array.isArray(response) ? response : 
                         response?.data?.rooms || response?.rooms || response?.data || [];
      
      const totalRooms = response?.total || response?.data?.total || roomsData.length;
      
      console.log('Fetched rooms:', roomsData);
      
      setRooms(roomsData);
      setPagination(prev => ({ ...prev, total: totalRooms }));
    } catch (error) {
      message.error('Failed to fetch rooms');
      console.error('Error fetching rooms:', error);
      setRooms([]);
    } finally {
      setLoading(false);
    }
  };

  const handleTableChange = (pagination) => {
    setPagination(pagination);
  };

  const handleSearch = (values) => {
    setName(values.name || '');
    setAssignedUserId(values.assigned_user_id || null);
    setPagination(prev => ({ ...prev, current: 1 })); // Reset to first page
    fetchRooms();
  };

  const handleReset = () => {
    form.resetFields();
    setName('');
    setAssignedUserId(null);
    setPagination(prev => ({ ...prev, current: 1 }));
    fetchRooms();
  };

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
      render: (date) => date ? new Date(date).toLocaleDateString() : 'N/A',
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
      ) : (
        <Table
          columns={columns}
          dataSource={Array.isArray(rooms) ? rooms.map(room => ({ ...room, key: room.id })) : []}
          loading={false}
          pagination={pagination}
          onChange={handleTableChange}
          rowClassName="room-row"
          locale={{
            emptyText: <Empty description="No rooms found" />
          }}
        />
      )}
    </div>
  );
};

export default RoomList;