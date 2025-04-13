import React, { useState, useEffect } from 'react';
import { Card, Tabs, Typography, Row, Col, Statistic, Spin } from 'antd';
import { 
  DesktopOutlined, 
  UserOutlined, 
  DashboardOutlined,
  TeamOutlined,
  BankOutlined
} from '@ant-design/icons';
import UsersListPage from '../user/UsersListPage';
import ComputersListPage from '../computer/ComputersListPage';
import userService from '../../services/user.service';
import roomService from '../../services/room.service';
import computerService from '../../services/computer.service';

const { Title } = Typography;

const AdminDashboard = () => {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [stats, setStats] = useState({
    users: { total: 0, loading: true },
    rooms: { total: 0, loading: true },
    computers: { total: 0, online: 0, loading: true }
  });

  useEffect(() => {
    if (activeTab === 'dashboard') {
      fetchStatistics();
    }
  }, [activeTab]);

  const fetchStatistics = async () => {
    try {
      // Fetch users
      setStats(prev => ({...prev, users: {...prev.users, loading: true}}));
      const usersData = await userService.getAllUsers();
      const users = usersData.data?.users || [];
      
      // Fetch rooms
      setStats(prev => ({...prev, rooms: {...prev.rooms, loading: true}}));
      const roomsData = await roomService.getAllRooms();
      const rooms = Array.isArray(roomsData) ? roomsData : (roomsData?.data?.rooms || []);
      
      // Fetch computers
      setStats(prev => ({...prev, computers: {...prev.computers, loading: true}}));
      const computersData = await computerService.getAllComputers();
      const computers = computersData?.data?.computers || [];
      const onlineComputers = computers.filter(c => c.status === 'online').length;
      
      setStats({
        users: { total: users.length, loading: false },
        rooms: { total: rooms.length, loading: false },
        computers: { 
          total: computers.length, 
          online: onlineComputers,
          loading: false 
        }
      });
      
    } catch (error) {
      console.error('Failed to fetch statistics:', error);
      setStats({
        users: { total: 0, loading: false },
        rooms: { total: 0, loading: false },
        computers: { total: 0, online: 0, loading: false }
      });
    }
  };

  // Define tab items
  const items = [
    {
      key: 'dashboard',
      label: (
        <span>
          <DashboardOutlined />
          Dashboard
        </span>
      ),
      children: (
        <div className="p-6">
          <div className="mb-8 pb-4 border-b border-gray-200">
            <Title level={2}>Admin Dashboard</Title>
            <p className="text-gray-600">Welcome to the Computer Management System administration panel</p>
          </div>
          
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={8}>
              <Card className="shadow-md hover:shadow-lg transition-shadow">
                <Statistic
                  title="Total Users"
                  value={stats.users.loading ? <Spin size="small" /> : stats.users.total}
                  prefix={<TeamOutlined />}
                />
              </Card>
            </Col>
            
            <Col xs={24} sm={8}>
              <Card className="shadow-md hover:shadow-lg transition-shadow">
                <Statistic
                  title="Total Rooms"
                  value={stats.rooms.loading ? <Spin size="small" /> : stats.rooms.total}
                  prefix={<BankOutlined />}
                />
              </Card>
            </Col>
            
            <Col xs={24} sm={8}>
              <Card className="shadow-md hover:shadow-lg transition-shadow">
                <Statistic
                  title="Total Computers"
                  value={stats.computers.loading ? <Spin size="small" /> : stats.computers.total}
                  prefix={<DesktopOutlined />}
                />
              </Card>
            </Col>
          </Row>
          
          <div className="mt-8">
            <Card title="System Status" className="shadow-md hover:shadow-lg transition-shadow">
              <Row gutter={[16, 16]}>
                <Col span={12}>
                  <div>
                    <h3 className="text-lg font-medium">Computers Online</h3>
                    <div className="flex items-center mt-2">
                      <div className="w-full bg-gray-200 rounded-full h-2.5 mr-2">
                        <div 
                          className="bg-green-600 h-2.5 rounded-full" 
                          style={{ 
                            width: stats.computers.loading ? '0%' : 
                              stats.computers.total === 0 ? '0%' : 
                              `${(stats.computers.online / stats.computers.total) * 100}%` 
                          }}
                        ></div>
                      </div>
                      <span className="text-sm font-medium">
                        {stats.computers.loading ? 
                          <Spin size="small" /> : 
                          `${stats.computers.online}/${stats.computers.total}`
                        }
                      </span>
                    </div>
                  </div>
                </Col>
                
                <Col span={12}>
                  <div>
                    <h3 className="text-lg font-medium">System Load</h3>
                    <div className="flex items-center mt-2">
                      <div className="w-full bg-gray-200 rounded-full h-2.5 mr-2">
                        <div 
                          className="bg-blue-600 h-2.5 rounded-full" 
                          style={{ width: '42%' }}
                        ></div>
                      </div>
                      <span className="text-sm font-medium">42%</span>
                    </div>
                  </div>
                </Col>
              </Row>
            </Card>
          </div>
        </div>
      ),
    },
    {
      key: 'computers',
      label: (
        <span>
          <DesktopOutlined />
          Computer Management
        </span>
      ),
      children: <ComputersListPage />,
    },
    {
      key: 'users',
      label: (
        <span>
          <UserOutlined />
          User Management
        </span>
      ),
      children: <UsersListPage />,
    },
  ];

  const handleTabChange = (key) => {
    setActiveTab(key);
  };

  return (
    <div className="admin-dashboard">
      <Tabs 
        defaultActiveKey="dashboard" 
        activeKey={activeTab}
        onChange={handleTabChange}
        items={items}
        size="large"
        tabBarStyle={{ marginBottom: 24 }}
      />
    </div>
  );
};

export default AdminDashboard;