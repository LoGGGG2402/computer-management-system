/**
 * @fileoverview Common data listing component with filtering and pagination
 * 
 * This component displays a list of data with filtering options,
 * pagination, and actions for viewing and editing items.
 * 
 * @module DataList
 */
import React from 'react';
import { Table, Button, Space, Empty, Tooltip, Form, Input, Select, Row, Col, Card, Tag } from 'antd';
import { EyeOutlined, EditOutlined, SearchOutlined, ReloadOutlined, DeleteOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { useAuth } from '../../contexts/AuthContext';
import Loading from './Loading';
import { useFormatting } from '../../hooks/useFormatting';

const { Option } = Select;

/**
 * DataList Component
 * 
 * A reusable component for displaying filterable, paginated tables with actions.
 * Supports different types of data (users, rooms, etc.) through configuration.
 *
 * @component
 * @param {Object} props - Component props
 * @param {string} props.type - Type of data being displayed ('user' or 'room')
 * @param {Array} props.data - Array of items to display
 * @param {boolean} props.loading - Loading state
 * @param {string} props.error - Error message if any
 * @param {Object} props.pagination - Pagination state
 * @param {Object} props.filters - Current filter values
 * @param {Array} props.filterOptions - Array of filter options for dropdown
 * @param {boolean} props.filterOptionsLoading - Loading state for filter options
 * @param {Function} props.onTableChange - Callback when table pagination changes
 * @param {Function} props.onSearch - Callback when search form is submitted
 * @param {Function} props.onReset - Callback when filters are reset
 * @param {Function} props.onEdit - Callback when edit button is clicked
 * @param {Function} props.onView - Callback when view button is clicked
 * @param {Function} props.onDeactivate - Callback when deactivate button is clicked
 * @param {Function} props.onReactivate - Callback when reactivate button is clicked
 * @returns {React.ReactElement} The rendered DataList component
 */
const DataList = ({ 
  type,
  data,
  loading,
  error,
  pagination,
  filters,
  filterOptions,
  filterOptionsLoading,
  onTableChange,
  onSearch,
  onReset,
  onEdit,
  onView,
  onDeactivate,
  onReactivate
}) => {
  const { isAdmin } = useAuth();
  const { formatTimestamp } = useFormatting();
  const [form] = Form.useForm();

  const getColumns = () => {
    const baseColumns = [
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
      }
    ];

    if (type === 'user') {
      return [
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
        }
      ];
    } else if (type === 'room') {
      return [
        {
          title: 'Room Name',
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
          width: 100,
          align: 'center',
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
          width: 120,
          render: (date) => formatTimestamp(date).split(' ')[0],
          sorter: (a, b) => new Date(a.created_at) - new Date(b.created_at),
        }
      ];
    }

    return baseColumns;
  };

  const getActionColumn = () => ({
    title: 'Actions',
    key: 'actions',
    align: 'center',
    render: (_, record) => {
      if (type === 'user') {
        const isActive = record.status === 'active' || record.is_active === true;
        return (
          <Space size="middle">
            <Button
              type="primary"
              icon={<EditOutlined />}
              onClick={() => onEdit(record)}
            >
              Edit
            </Button>
            {isActive ? (
              <Button
                type="default"
                danger
                icon={<DeleteOutlined />}
                onClick={() => onDeactivate(record.id)}
              >
                Deactivate
              </Button>
            ) : (
              <Button
                type="primary"
                icon={<CheckCircleOutlined />}
                style={{ backgroundColor: '#52c41a', borderColor: '#52c41a' }}
                onClick={() => onReactivate(record.id)}
              >
                Reactivate
              </Button>
            )}
          </Space>
        );
      } else {
        return (
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
        );
      }
    },
  });

  const columns = [...getColumns(), getActionColumn()];

  const getFilterFields = () => {
    const baseFields = [
      <Col xs={24} sm={12} key="name">
        <Form.Item name={type === 'user' ? 'username' : 'name'} label={`${type === 'user' ? 'Username' : 'Room'} Name`}>
          <Input placeholder={`Search by ${type === 'user' ? 'username' : 'room name'}`} prefix={<SearchOutlined />} />
        </Form.Item>
      </Col>
    ];

    if (type === 'user') {
      baseFields.push(
        <Col xs={24} sm={12} key="role">
          <Form.Item name="role" label="Role">
            <Select placeholder="Select a role" allowClear>
              <Option value="admin">Admin</Option>
              <Option value="user">User</Option>
            </Select>
          </Form.Item>
        </Col>,
        <Col xs={24} sm={12} key="status">
          <Form.Item name="is_active" label="Status">
            <Select placeholder="Select status" allowClear>
              <Option value="true">Active</Option>
              <Option value="false">Inactive</Option>
            </Select>
          </Form.Item>
        </Col>
      );
    } else if (type === 'room' && isAdmin) {
      baseFields.push(
        <Col xs={24} sm={12} key="assigned_user">
          <Form.Item name="assigned_user_id" label="Assigned User">
            <Select
              placeholder="Filter by assigned user"
              allowClear
              showSearch
              optionFilterProp="children"
              loading={filterOptionsLoading}
              filterOption={(input, option) =>
                option.children.toLowerCase().includes(input.toLowerCase())
              }
            >
              {filterOptions.map(user => (
                <Option key={user.id} value={user.id}>
                  {user.username} {user.firstName && user.lastName ? `(${user.firstName} ${user.lastName})` : ''}
                </Option>
              ))}
            </Select>
          </Form.Item>
        </Col>
      );
    }

    return baseFields;
  };

  return (
    <div className={`${type}-list`}>
      <Card className="filter-card" style={{ marginBottom: '16px' }}>
        <Form
          form={form}
          name={`${type}_filter`}
          onFinish={onSearch}
          layout="vertical"
          initialValues={filters}
        >
          <Row gutter={16}>
            {getFilterFields()}
          </Row>
          <Row>
            <Col span={24} style={{ textAlign: 'right' }}>
              <Space>
                <Button onClick={onReset} icon={<ReloadOutlined />}>
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
        <Loading type="section" tip={`Loading ${type} list...`} />
      ) : error ? (
        <Empty description={error || `Failed to load ${type}s`} />
      ) : (
        <Table
          columns={columns}
          dataSource={Array.isArray(data) ? data.map(item => ({ ...item, key: item.id })) : []}
          loading={loading}
          pagination={pagination}
          onChange={onTableChange}
          rowClassName={`${type}-row`}
          locale={{
            emptyText: <Empty description={`No ${type}s found matching your criteria`} />
          }}
        />
      )}
    </div>
  );
};

export default DataList; 