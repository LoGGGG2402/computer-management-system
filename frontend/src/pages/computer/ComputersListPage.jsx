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
import ComputerCard from '../../components/room/ComputerCard';
import { LoadingComponent } from '../../components/common';
import {
  useAppDispatch,
  useAppSelector,
  fetchComputers,
  selectComputers,
  selectComputerLoading,
  selectComputerPagination,
  setComputerCurrentPage
} from '../../app/index';

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
  const dispatch = useAppDispatch();
  
  // Redux selectors
  const computers = useAppSelector(selectComputers);
  const loading = useAppSelector(selectComputerLoading);
  const pagination = useAppSelector(selectComputerPagination);
  
  // Local state for filters
  const [searchName, setSearchName] = useState('');
  const [filterHasErrors, setFilterHasErrors] = useState(false);
  const [pageSize, setPageSize] = useState(12);

  /**
   * Fetches computers from the API when dependencies change
   */
  useEffect(() => {
    const filters = {
      page: pagination.currentPage,
      limit: pageSize,
      name: searchName || undefined,
      has_errors: filterHasErrors || undefined
    };
    
    dispatch(fetchComputers(filters));
  }, [dispatch, pagination.currentPage, pageSize, searchName, filterHasErrors]);

  /**
   * Handles name search input
   */
  const handleNameSearch = (value) => {
    setSearchName(value);
    dispatch(setComputerCurrentPage(1));
  };

  /**
   * Handles error filter change
   */
  const handleErrorFilterChange = (e) => {
    setFilterHasErrors(e.target.checked);
    dispatch(setComputerCurrentPage(1));
  };

  /**
   * Handles navigation to computer details page
   */
  const handleView = (computerId) => {
    navigate(`/computers/${computerId}`);
  };

  /**
   * Triggers a refresh of the computers list
   */
  const handleRefresh = () => {
    setSearchName('');
    setFilterHasErrors(false);
    dispatch(setComputerCurrentPage(1));
    message.success('Computer list refreshed');
  };

  /**
   * Handles pagination changes
   */
  const handlePagination = (page, pageSize) => {
    dispatch(setComputerCurrentPage(page));
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
                  <ComputerCard 
                    computer={computer} 
                    onView={handleView}
                    onRefresh={handleRefresh}
                  />
                </Col>
              ))}
            </Row>
            
            {pagination.total > 0 && (
              <div style={{ marginTop: '20px', textAlign: 'center' }}>
                <Pagination
                  current={pagination.currentPage}
                  pageSize={pageSize}
                  total={pagination.total}
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