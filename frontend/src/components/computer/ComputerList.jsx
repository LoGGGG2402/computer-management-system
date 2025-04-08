import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { Table, Button, Space, message, Empty, Tag, Badge, Tooltip, Form, Input, Select, Row, Col, Card, Checkbox } from 'antd';
import { EyeOutlined, SearchOutlined, ReloadOutlined, FilterOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import computerService from '../../services/computer.service';
import roomService from '../../services/room.service';

const { Option } = Select;

const ComputerList = ({ 
  computers: propComputers, 
  onView, 
  refreshTrigger, 
  hideRoomColumn = false, 
  pagination: propPagination, 
  onChange: propOnChange, 
  loading: propLoading 
}) => {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  
  // State management
  const [computers, setComputers] = useState([]);
  const [filteredComputers, setFilteredComputers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [rooms, setRooms] = useState([]);
  const [filters, setFilters] = useState({ name: '', status: null, has_errors: null });
  const [isFiltering, setIsFiltering] = useState(false);

  // Determine if we're in standalone mode or using parent component's data
  const isStandalone = propComputers === undefined;

  // Helper functions
  const formatRAMSize = bytes => !bytes ? 'Unknown' : `${(parseInt(bytes) / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  
  const formatTimestamp = timestamp => {
    if (!timestamp) return 'Never';
    const date = new Date(timestamp);
    return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
  };
  
  const isComputerOnline = computer => 
    computer.status === 'online' || 
    (computer.last_seen && (new Date() - new Date(computer.last_seen) < 5 * 60 * 1000));

  const hasComputerErrors = computer => 
    computer.has_errors === true || (computer.errors && computer.errors.length > 0);

  // Apply filters to the current set of computers
  const applyFilters = useCallback((computersToFilter) => {
    if (!computersToFilter || !Array.isArray(computersToFilter)) return [];
    
    return computersToFilter.filter(computer => {
      const nameMatch = !filters.name || 
        computer.name.toLowerCase().includes(filters.name.toLowerCase());
      
      const statusMatch = !filters.status || 
        (filters.status === 'online' && isComputerOnline(computer)) ||
        (filters.status === 'offline' && !isComputerOnline(computer));
      
      const errorsMatch = !filters.has_errors || hasComputerErrors(computer);
      
      return nameMatch && statusMatch && errorsMatch;
    });
  }, [filters]);

  // For client-side pagination, slice the data
  const getPaginatedData = useCallback((data) => {
    if (!isFiltering || !data || !Array.isArray(data)) return data;
    
    const startIndex = (pagination.current - 1) * (pagination.pageSize || 10);
    const endIndex = startIndex + (pagination.pageSize || 10);
    return data.slice(startIndex, endIndex);
  }, [isFiltering, pagination.current, pagination.pageSize]);

  // Effect for handling filters in prop-based mode
  useEffect(() => {
    if (!isStandalone && propComputers) {
      const filtered = applyFilters(propComputers);
      setFilteredComputers(filtered);
      setIsFiltering(filters.name !== '' || filters.status !== null || filters.has_errors !== null);
    }
  }, [propComputers, filters, isStandalone, applyFilters]);

  // Effect for standalone mode
  useEffect(() => {
    if (isStandalone) {
      fetchComputers();
      fetchRooms();
    }
  }, [refreshTrigger, isStandalone, pagination.current, pagination.pageSize, filters]);

  // Network requests
  const fetchComputers = async () => {
    try {
      setLoading(true);
      const response = await computerService.getAllComputers(
        pagination.current,
        pagination.pageSize,
        filters
      );
      
      // Extract computers data from various response formats
      let computersData = [];
      let totalComputers = 0;
      
      if (response?.data?.computers && Array.isArray(response.data.computers)) {
        computersData = response.data.computers;
        totalComputers = response.data.total || computersData.length;
      } else if (response?.data && Array.isArray(response.data)) {
        computersData = response.data;
        totalComputers = response.total || computersData.length;
      } else if (response?.computers && Array.isArray(response.computers)) {
        computersData = response.computers;
        totalComputers = response.total || computersData.length;
      } else if (Array.isArray(response)) {
        computersData = response;
        totalComputers = computersData.length;
      }
      
      setComputers(computersData || []);
      setPagination(prev => ({ ...prev, total: totalComputers }));
    } catch (error) {
      message.error('Failed to fetch computers');
      console.error('Error fetching computers:', error);
      setComputers([]);
    } finally {
      setLoading(false);
    }
  };

  const fetchRooms = async () => {
    try {
      const response = await roomService.getAllRooms();
      const roomsData = response?.data?.rooms || response?.data || response?.rooms || response || [];
      setRooms(Array.isArray(roomsData) ? roomsData : []);
    } catch (error) {
      console.error('Error fetching rooms:', error);
    }
  };

  // Event handlers
  const handleSearch = values => {
    const newFilters = {
      name: values.name || '',
      status: values.status || null,
      has_errors: values.has_errors === true ? true : null
    };
    
    setFilters(newFilters);
    
    if (isStandalone) {
      setPagination(prev => ({ ...prev, current: 1 }));
    } else if (propOnChange && !isFiltering) {
      propOnChange({
        ...propPagination,
        current: 1,
        filters: newFilters
      });
    }
  };

  const handleReset = () => {
    form.resetFields();
    const newFilters = { name: '', status: null, has_errors: null };
    
    setFilters(newFilters);
    setIsFiltering(false);
    
    if (isStandalone) {
      setPagination(prev => ({ ...prev, current: 1 }));
    } else if (propOnChange) {
      propOnChange({
        ...propPagination,
        current: 1,
        filters: newFilters
      });
    }
  };

  const handleTableChange = newPagination => {
    if (isStandalone) {
      setPagination(newPagination);
    } else if (propOnChange && !isFiltering) {
      propOnChange(newPagination);
    } else if (isFiltering) {
      setPagination(prev => ({
        ...prev,
        current: newPagination.current,
        pageSize: newPagination.pageSize
      }));
    }
  };

  // Handle navigation to ComputerDetailPage
  const handleViewComputer = (computerId) => {
    if (onView) {
      // Use the provided onView function if it exists
      onView(computerId);
    } else {
      // Otherwise, navigate directly to the ComputerDetailPage
      navigate(`/computers/${computerId}`);
    }
  };

  // UI Components
  const getOnlineStatus = computer => (
    <Space>
      <Badge status={isComputerOnline(computer) ? "success" : "error"} />
      {isComputerOnline(computer) ? 
        <Tag color="green">Online</Tag> : 
        <Tag color="red">Offline</Tag>}
    </Space>
  );

  // Table column definitions
  const allColumns = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (text, record) => (
        <Space>
          {text}
          {hasComputerErrors(record) && (
            <Tooltip title="This computer has errors">
              <Tag color="red">Has Errors</Tag>
            </Tooltip>
          )}
        </Space>
      )
    },
    {
      title: 'Status',
      key: 'status',
      render: (_, record) => getOnlineStatus(record),
      sorter: (a, b) => {
        const aOnline = isComputerOnline(a);
        const bOnline = isComputerOnline(b);
        return (aOnline === bOnline) ? 0 : aOnline ? -1 : 1;
      },
    },
    {
      title: 'IP Address',
      dataIndex: 'ip_address',
      key: 'ip_address',
      render: ip => ip || <Tag color="default">Not set</Tag>,
    },
    {
      title: 'Hardware',
      key: 'hardware',
      render: (_, record) => (
        <Tooltip title={`CPU: ${record.cpu_info || 'Unknown'}`}>
          <span>
            {record.cpu_info ? 
              `${record.cpu_info.split(' ').slice(0, 2).join(' ')}...` : 
              'Unknown CPU'} | {formatRAMSize(record.total_ram)}
          </span>
        </Tooltip>
      )
    },
    {
      title: 'Room',
      dataIndex: ['room', 'name'],
      key: 'room',
      render: (text, record) => record.room?.name || 'Unknown',
    },
    {
      title: 'Last Seen',
      key: 'last_seen',
      render: (_, record) => formatTimestamp(record.last_seen),
      sorter: (a, b) => {
        if (!a.last_seen && !b.last_seen) return 0;
        if (!a.last_seen) return 1;
        if (!b.last_seen) return -1;
        return new Date(b.last_seen) - new Date(a.last_seen);
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
            onClick={() => handleViewComputer(record.id)}
          >
            View
          </Button>
        </Space>
      ),
    },
  ];

  // Filter out the Room column if hideRoomColumn is true
  const columns = useMemo(() => 
    hideRoomColumn ? allColumns.filter(column => column.key !== 'room') : allColumns,
    [hideRoomColumn]
  );

  // Compute display values
  const displayComputers = isStandalone ? computers : (isFiltering ? filteredComputers : propComputers);
  const displayLoading = isStandalone ? loading : propLoading;
  
  // Adjust pagination for client-side filtering
  const displayPagination = isStandalone ? pagination : 
    (isFiltering ? { 
      ...pagination, 
      total: filteredComputers.length,
      current: pagination.current,
      pageSize: pagination.pageSize || 10
    } : propPagination);

  // Final data to display with pagination applied
  const finalDisplayComputers = useMemo(() => 
    isFiltering ? getPaginatedData(filteredComputers) : displayComputers,
    [isFiltering, filteredComputers, displayComputers, getPaginatedData]
  );

  // Render the filter form
  const renderFilterForm = () => (
    <Card className="filter-card" style={{ marginBottom: '16px' }}>
      <Form 
        form={form}
        name="computer_filter"
        onFinish={handleSearch}
        layout="vertical"
        initialValues={{ name: '', status: null, has_errors: null }}
      >
        <Row gutter={16}>
          <Col xs={24} sm={12} md={8}>
            <Form.Item name="name" label="Computer Name">
              <Input placeholder="Search by name" prefix={<SearchOutlined />} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={8}>
            <Form.Item name="status" label="Status">
              <Select placeholder="Filter by status" allowClear>
                <Option value="online">Online</Option>
                <Option value="offline">Offline</Option>
              </Select>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12} md={8}>
            <Form.Item name="has_errors" label="Errors" valuePropName="checked">
              <Checkbox>Show only computers with errors</Checkbox>
            </Form.Item>
          </Col>
        </Row>
        <Row>
          <Col span={24} style={{ textAlign: 'right' }}>
            <Space>
              <Button onClick={handleReset} icon={<ReloadOutlined />}>
                Reset
              </Button>
              <Button type="primary" htmlType="submit" icon={<FilterOutlined />}>
                Filter
              </Button>
            </Space>
          </Col>
        </Row>
      </Form>
    </Card>
  );

  return (
    <div className="computer-list">
      {renderFilterForm()}

      <Table
        columns={columns}
        dataSource={Array.isArray(finalDisplayComputers) ? 
          finalDisplayComputers.map(computer => ({ ...computer, key: computer.id })) : 
          []
        }
        loading={displayLoading}
        pagination={displayPagination}
        onChange={handleTableChange}
        rowClassName={(record) => hasComputerErrors(record) ? 'computer-row error-row' : 'computer-row'}
        locale={{ emptyText: <Empty description="No computers found" /> }}
      />
    </div>
  );
};

export default ComputerList;
