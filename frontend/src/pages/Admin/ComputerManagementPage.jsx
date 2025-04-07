import React, { useState, useEffect } from 'react';
import { Card, Button, Modal, Typography, message, Tabs, Space, Input } from 'antd';
import { PlusOutlined, DesktopOutlined, SearchOutlined } from '@ant-design/icons';
import ComputerList from '../../components/computer/ComputerList';
import computerService from '../../services/computer.service';
import roomService from '../../services/room.service';

const { Title } = Typography;

const ComputerManagementPage = () => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedComputer, setSelectedComputer] = useState(null);
  const [modalAction, setModalAction] = useState('create'); // 'create', 'edit', 'view'
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  const [selectedComputerId, setSelectedComputerId] = useState(null);
  
  // Computer data state
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  
  // Room data for reference only
  const [rooms, setRooms] = useState([]);
  const [searchName, setSearchName] = useState('');

  // Fetch rooms for reference only (not for filtering)
  useEffect(() => {
    fetchRooms();
  }, []);

  // Fetch computers when page changes or refreshTrigger updates
  useEffect(() => {
    fetchComputers();
  }, [currentPage, pageSize, searchName, refreshTrigger]);

  const fetchRooms = async () => {
    try {
      const roomsData = await roomService.getAllRooms();
      const rooms = Array.isArray(roomsData) ? roomsData : 
                   (roomsData?.data?.rooms || []);
      setRooms(rooms);
    } catch (error) {
      console.error('Error fetching rooms:', error);
      message.error('Failed to load rooms for filtering');
    }
  };

  const fetchComputers = async () => {
    try {
      setLoading(true);
      let response;
      const filters = {};
      
      // Add name filter if provided
      if (searchName) {
        filters.name = searchName;
      }

      // Fetch computers with pagination
      response = await computerService.getAllComputers(currentPage, pageSize, filters);

      // Handle the returned data structure
      if (response?.status === 'success' && response?.data) {
        const { computers, total, currentPage: returnedPage, totalPages } = response.data;
        setComputers(computers || []);
        setTotal(total || 0);
        
        // Adjust current page if the returned page is different
        if (returnedPage && returnedPage !== currentPage) {
          setCurrentPage(returnedPage);
        }
      } else {
        setComputers([]);
        setTotal(0);
      }
    } catch (error) {
      console.error('Error fetching computers:', error);
      message.error('Failed to load computers');
      setComputers([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  };

  const handleNameSearch = (value) => {
    setSearchName(value);
    setCurrentPage(1); // Reset to first page when search changes
  };

  const handleTableChange = (pagination) => {
    setCurrentPage(pagination.current);
    setPageSize(pagination.pageSize);
  };

  const handleView = (computerId) => {
    setSelectedComputerId(computerId);
    setModalAction('view');
    setIsModalVisible(true);
  };

  const handleEdit = async (computerId) => {
    try {
      const response = await computerService.getComputerById(computerId);
      const computer = response?.data || response;
      setSelectedComputer(computer);
      setModalAction('edit');
      setIsModalVisible(true);
    } catch (error) {
      console.error('Error fetching computer details:', error);
      message.error('Failed to load computer details');
    }
  };

  const handleDelete = async (computerId) => {
    try {
      await computerService.deleteComputer(computerId);
      message.success('Computer deleted successfully');
      // Refresh the computer list
      setRefreshTrigger(prev => prev + 1);
    } catch (error) {
      console.error('Error deleting computer:', error);
      message.error('Failed to delete computer');
    }
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  const handleSuccess = () => {
    setIsModalVisible(false);
    // Trigger computer list refresh
    setRefreshTrigger(prev => prev + 1);
    message.success(`Computer ${modalAction === 'edit' ? 'updated' : 'action completed'} successfully`);
  };

  const modalTitle = 
    modalAction === 'create' ? 'Add New Computer' :
    modalAction === 'edit' ? 'Edit Computer' : 
    'Computer Details';

  const tabItems = [
    {
      key: 'details',
      label: 'Details',
      children: (
        <div className="computer-details">
          {/* Computer details will be implemented here */}
          <p>Computer ID: {selectedComputerId}</p>
          <p>Details coming soon...</p>
        </div>
      )
    },
    {
      key: 'errors',
      label: 'Error Reports',
      children: (
        <div className="computer-errors">
          <p>Error history will be shown here</p>
        </div>
      )
    }
  ];

  return (
    <div className="computer-management-page">
      <Card
        title={
          <div className="flex items-center">
            <DesktopOutlined style={{ marginRight: '8px', fontSize: '24px' }} />
            <Title level={3}>Computer Management</Title>
          </div>
        }
        extra={
          <Space>
            <Input.Search
              placeholder="Search by name"
              allowClear
              onSearch={handleNameSearch}
              style={{ width: 200 }}
              enterButton={<SearchOutlined />}
            />
            <Button 
              type="primary" 
              icon={<PlusOutlined />}
              onClick={() => {
                setModalAction('create');
                setSelectedComputer(null);
                setIsModalVisible(true);
              }}
            >
              Add Computer
            </Button>
          </Space>
        }
      >
        <ComputerList 
          computers={computers}
          loading={loading}
          onViewDetails={handleView}
          onEdit={handleEdit}
          onDelete={handleDelete}
          pagination={{
            current: currentPage,
            pageSize: pageSize,
            total: total,
            onChange: (page, pageSize) => {
              setCurrentPage(page);
              setPageSize(pageSize);
            },
            showSizeChanger: true,
            showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} items`
          }}
          onChange={handleTableChange}
        />
      </Card>

      <Modal
        title={modalTitle}
        open={isModalVisible}
        onCancel={handleCancel}
        footer={null}
        width={800}
      >
        {modalAction === 'view' ? (
          <Tabs defaultActiveKey="details" items={tabItems} />
        ) : (
          <div>
            {/* Computer edit form will be implemented here */}
            <p>Edit form coming soon...</p>
            <Button type="primary" onClick={handleSuccess}>
              Save Changes
            </Button>
          </div>
        )}
      </Modal>
    </div>
  );
};

export default ComputerManagementPage;