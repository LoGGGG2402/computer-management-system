/**
 * @fileoverview Admin Dashboard component for the Computer Management System
 * 
 * This component serves as the main control panel for administrators, providing
 * system-wide statistics and overview, including unresolved errors.
 * 
 * @module AdminDashboard
 */
import React from 'react';
import { Card, Typography, Row, Col, Statistic, List, Tag, Tooltip } from 'antd';
import { 
  DesktopOutlined, 
  TeamOutlined,
  BankOutlined,
  WarningOutlined,
  CloudServerOutlined,
  ApiOutlined,
  InfoCircleOutlined
} from '@ant-design/icons';
import staticsService from '../../services/statics.service';
import { LoadingComponent } from '../../components/common';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';
import { useFormatting } from '../../hooks/useFormatting';

const { Title, Text } = Typography;

/**
 * Admin Dashboard Component
 * 
 * Provides administrative interface with:
 * - Overview statistics (users, rooms, computers)
 * - System status indicators
 * - List of unresolved computer errors
 * 
 * @component
 * @returns {React.ReactElement} The rendered AdminDashboard component
 */
const AdminDashboard = () => {
  // Use hooks for fetching and formatting
  const { data: stats, loading } = useSimpleFetch(
    staticsService.getSystemStats,
    [], // Fetch on mount
    { errorMessage: 'Failed to fetch statistics' }
  );
  const { formatTimestamp, getTimeAgo } = useFormatting();

  // Default stats structure
  const defaultStats = {
    totalUsers: 0,
    totalRooms: 0,
    totalComputers: 0,
    onlineComputers: 0,
    offlineComputers: 0,
    computersWithErrors: 0,
    unresolvedErrors: [],
  };

  // Use fetched stats or default if loading/error
  const displayStats = stats || defaultStats;
  const unresolvedErrors = displayStats.unresolvedErrors || [];

  return (
    <div className="admin-dashboard p-6"> 
      <div className="mb-8 pb-4 border-b border-gray-200">
        <Title level={2}>Admin Dashboard</Title>
        <p className="text-gray-600">Welcome to the Computer Management System administration panel</p>
      </div>
      
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} md={6}>
          <Card className="shadow-md hover:shadow-lg transition-shadow">
            <Statistic
              title="Total Users"
              value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.totalUsers}
              prefix={<TeamOutlined />}
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} md={6}>
          <Card className="shadow-md hover:shadow-lg transition-shadow">
            <Statistic
              title="Total Rooms"
              value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.totalRooms}
              prefix={<BankOutlined />}
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} md={6}>
          <Card className="shadow-md hover:shadow-lg transition-shadow">
            <Statistic
              title="Total Computers"
              value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.totalComputers}
              prefix={<DesktopOutlined />}
            />
          </Card>
        </Col>

        <Col xs={24} sm={12} md={6}>
          <Card className="shadow-md hover:shadow-lg transition-shadow">
            <Statistic
              title="Computers with Errors"
              value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.computersWithErrors}
              prefix={<WarningOutlined />}
              valueStyle={{ color: displayStats.computersWithErrors > 0 ? '#cf1322' : undefined }}
            />
          </Card>
        </Col>
      </Row>
      
      <div className="mt-8">
        <Card title="System Status" className="shadow-md hover:shadow-lg transition-shadow">
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12}>
              <Card bordered={false}>
                <Statistic
                  title="Online Computers"
                  value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.onlineComputers}
                  prefix={<CloudServerOutlined style={{ color: '#3f8600' }} />}
                  suffix={`/ ${displayStats.totalComputers}`}
                />
                <div className="w-full bg-gray-200 rounded-full h-2.5 mt-2">
                  <div 
                    className="bg-green-600 h-2.5 rounded-full" 
                    style={{ 
                      width: loading || displayStats.totalComputers === 0 ? '0%' : 
                      `${(displayStats.onlineComputers / displayStats.totalComputers) * 100}%` 
                    }}
                  ></div>
                </div>
              </Card>
            </Col>
            
            <Col xs={24} sm={12}>
              <Card bordered={false}>
                <Statistic
                  title="Offline Computers"
                  value={loading ? <LoadingComponent type="inline" size="small" tip="" /> : displayStats.offlineComputers}
                  prefix={<ApiOutlined style={{ color: '#cf1322' }} />}
                  suffix={`/ ${displayStats.totalComputers}`}
                />
                <div className="w-full bg-gray-200 rounded-full h-2.5 mt-2">
                  <div 
                    className="bg-red-600 h-2.5 rounded-full" 
                    style={{ 
                      width: loading || displayStats.totalComputers === 0 ? '0%' : 
                      `${(displayStats.offlineComputers / displayStats.totalComputers) * 100}%` 
                    }}
                  ></div>
                </div>
              </Card>
            </Col>
          </Row>
        </Card>
      </div>

      <div className="mt-8">
        <Card 
          title={<><WarningOutlined style={{ color: '#faad14', marginRight: 8 }} /> Unresolved Errors</>} 
          className="shadow-md hover:shadow-lg transition-shadow"
          extra={loading ? <LoadingComponent type="inline" size="small" tip="" /> : <Tag color="volcano">{unresolvedErrors.length} Active</Tag>}
        >
          {loading ? (
            <LoadingComponent tip="Loading errors..." />
          ) : unresolvedErrors.length === 0 ? (
            <Text type="secondary">No unresolved errors found.</Text>
          ) : (
            <List
              itemLayout="horizontal"
              dataSource={unresolvedErrors}
              renderItem={item => (
                <List.Item
                  actions={[
                    <Tooltip title={`Reported at: ${formatTimestamp(item.reported_at)}`}>
                      <Text type="secondary">{getTimeAgo(item.reported_at)}</Text>
                    </Tooltip>
                  ]}
                >
                  <List.Item.Meta
                    avatar={<WarningOutlined style={{ color: '#faad14', fontSize: '1.5em' }} />}
                    title={<Text strong>{item.computerName || `Computer ID: ${item.computerId}`}</Text>}
                    description={
                      <>
                        <Tag color="red">{item.error_type || 'General'}</Tag>
                        {item.error_message}
                        {item.error_details && Object.keys(item.error_details).length > 0 && (
                          <Tooltip title={<pre style={{ margin: 0, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>{JSON.stringify(item.error_details, null, 2)}</pre>}>
                            <InfoCircleOutlined style={{ marginLeft: 8, cursor: 'pointer', color: '#1890ff' }} />
                          </Tooltip>
                        )}
                      </>
                    }
                  />
                </List.Item>
              )}
            />
          )}
        </Card>
      </div>
    </div>
  );
};

export default AdminDashboard;