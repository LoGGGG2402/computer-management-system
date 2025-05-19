/**
 * @fileoverview Page component displaying a list of computers with filtering and pagination
 * 
 * This component provides a comprehensive view of all computers in the system,
 * with search functionality, pagination, and links to detailed computer views.
 * 
 * @module ComputersListPage
 */
import React, { useState, useEffect } from 'react';
import { Card, Button, Typography, message, Space, Input, Row, Col, Pagination, Checkbox } from 'antd';
import { DesktopOutlined, SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import SimpleComputerCard from '../../components/computer/SimpleComputerCard';
import computerService from '../../services/computer.service';
import { LoadingComponent } from '../../components/common';

const { Title } = Typography;

/**
 * Computers List Page Component
 * 
 * Displays a searchable, paginated grid of computers in the system.
 * Allows filtering by name and error state, and provides links to detailed computer views.
 * 
 * @component
 * @returns {React.ReactElement} The rendered ComputersListPage component
 */
const ComputersListPage = () => {
  const navigate = useNavigate();
  const [refreshTrigger, setRefreshTrigger] = useState(0);
  
  // Computer data state
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(12); // 12 items per page for better grid layout

  // Filter states
  const [searchName, setSearchName] = useState('');
  const [filterHasErrors, setFilterHasErrors] = useState(false);

  /**
   * Fetches computers from the API when dependencies change
   * 
   * @effect
   * @dependency {number} currentPage - Current page number
   * @dependency {number} pageSize - Number of items per page
   * @dependency {string} searchName - Name search filter
   * @dependency {boolean} filterHasErrors - Has errors filter
   * @dependency {number} refreshTrigger - Counter to trigger refreshes
   */
  useEffect(() => {
    fetchComputers();
  }, [currentPage, pageSize, searchName, filterHasErrors, refreshTrigger]);

  /**
   * Fetches computers from the API with pagination and filters
   * 
   * @function
   * @async
   */
  const fetchComputers = async () => {
    try {
      setLoading(true);
      
      // Create filters object for API call
      const filters = {
        page: currentPage,
        limit: pageSize
      };
      
      // Add filters if they have values
      if (searchName) filters.name = searchName;
      if (filterHasErrors) filters.has_errors = true;

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

  /**
   * Handles name search input
   * 
   * @function
   * @param {string} value - The search term entered by the user
   */
  const handleNameSearch = (value) => {
    setSearchName(value);
    setCurrentPage(1); // Reset to first page when search changes
  };

  /**
   * Handles error filter change
   * @param {Event} e - Checkbox change event
   */
  const handleErrorFilterChange = (e) => {
    setFilterHasErrors(e.target.checked);
    setCurrentPage(1);
  };

  /**
   * Handles navigation to computer details page
   * 
   * @function
   * @param {number|string} computerId - ID of the computer to view
   */
  const handleView = (computerId) => {
    // Navigate to computer details page instead of showing modal
    navigate(`/computers/${computerId}`);
  };

  /**
   * Triggers a refresh of the computers list
   * 
   * @function
   */
  const handleRefresh = () => {
    // Reset filters and trigger refresh
    setSearchName('');
    setFilterHasErrors(false);
    setCurrentPage(1); // Go back to page 1 on full refresh
    setRefreshTrigger(prev => prev + 1);
    message.success('Computer list refreshed');
  };

  /**
   * Handles pagination changes
   * 
   * @function
   * @param {number} page - New page number
   * @param {number} pageSize - New page size
   */
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
            <Title level={3} style={{ marginBottom: 0 }}>Computer Management</Title>
          </div>
        }
        extra={
          <Space wrap>
            <Input.Search
              placeholder="Search by name"
              allowClear
              value={searchName}
              onChange={(e) => setSearchName(e.target.value)}
              onSearch={handleNameSearch}
              style={{ width: 200 }}
              enterButton={<SearchOutlined />}
            />
            <Checkbox
              checked={filterHasErrors}
              onChange={handleErrorFilterChange}
            >
              Has Errors
            </Checkbox>
            <Button
              type="primary"
              onClick={handleRefresh}
            >
              Refresh / Clear Filters
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
                    onRefresh={() => setRefreshTrigger(prev => prev + 1)}
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
                <Typography.Text type="secondary">No computers found matching the criteria</Typography.Text>
              </div>
            )}
          </>
        )}
      </Card>
    </div>
  );
};

export default ComputersListPage;