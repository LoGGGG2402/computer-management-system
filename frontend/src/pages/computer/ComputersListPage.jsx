import React, { useState, useEffect } from 'react';
import { Card, Button, Typography, message, Space, Input, Row, Col, Pagination } from 'antd';
import { DesktopOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import SimpleComputerCard from '../../components/computer/SimpleComputerCard';
import computerService from '../../services/computer.service';
import { LoadingComponent } from '../../components/common';

const { Title } = Typography;

const ComputersListPage = () => {
  const navigate = useNavigate();
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  
  // Computer data state
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(12); // 12 items per page for better grid layout
  const [searchName, setSearchName] = useState('');

  // Fetch computers when page changes or refreshTrigger updates
  useEffect(() => {
    fetchComputers();
  }, [currentPage, pageSize, searchName, refreshTrigger]);

  const fetchComputers = async () => {
    try {
      setLoading(true);
      
      // Create filters object for API call
      const filters = {
        page: currentPage,
        limit: pageSize
      };
      
      // Add name filter if provided
      if (searchName) {
        filters.name = searchName;
      }

      // Fetch computers with pagination and filters
      const response = await computerService.getAllComputers(filters);

      // Handle the returned data structure
      if (response) {
        const computers = Array.isArray(response) ? response : 
                          (response?.computers || response?.data?.computers || []);
        const total = response?.total || response?.data?.total || computers.length;
        
        setComputers(computers || []);
        setTotal(total || 0);
        
        // Adjust current page if needed
        if (response?.currentPage && response.currentPage !== currentPage) {
          setCurrentPage(response.currentPage);
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

  const handleView = (computerId) => {
    // Navigate to computer details page instead of showing modal
    navigate(`/computers/${computerId}`);
  };

  const handleRefresh = () => {
    // Trigger computer list refresh
    setRefreshTrigger(prev => prev + 1);
    message.success('Computer list refreshed');
  };

  const handlePagination = (page, pageSize) => {
    setCurrentPage(page);
    setPageSize(pageSize);
  };

  return (
    <div className="computer-management-page">
      <Card
        title={
          <div style={{ display: 'flex', alignItems: 'center' }}>
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
              onClick={handleRefresh}
            >
              Refresh
            </Button>
          </Space>
        }
      >
        {loading ? (
          <LoadingComponent type="section" tip="Loading computers list..." />
        ) : (
          <>
            <Row gutter={[16, 16]}>
              {computers.map(computer => (
                <Col xs={24} sm={12} md={8} lg={6} key={computer.id}>
                  <SimpleComputerCard 
                    computer={computer} 
                    onView={handleView}
                    onRefresh={handleRefresh}
                  />
                </Col>
              ))}
            </Row>
            
            {total > 0 && (
              <div style={{ marginTop: '20px', textAlign: 'center' }}>
                <Pagination
                  current={currentPage}
                  pageSize={pageSize}
                  total={total}
                  onChange={handlePagination}
                  showSizeChanger
                  pageSizeOptions={['12', '24', '36', '48']}
                  showTotal={(total, range) => `${range[0]}-${range[1]} of ${total} items`}
                />
              </div>
            )}
            
            {computers.length === 0 && !loading && (
              <div style={{ textAlign: 'center', padding: '20px' }}>
                <Typography.Text type="secondary">No computers found</Typography.Text>
              </div>
            )}
          </>
        )}
      </Card>
    </div>
  );
};

export default ComputersListPage;