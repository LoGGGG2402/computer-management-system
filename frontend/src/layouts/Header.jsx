/**
 * @fileoverview Main navigation header component
 * 
 * This component handles the top navigation bar of the application,
 * providing links to main sections and user authentication controls.
 * It has responsive design with mobile menu support.
 * 
 * @module Header
 */
import React, { useState } from 'react';
import { Layout, Menu, Button, Dropdown, Avatar, Badge, Space, Typography } from 'antd';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  LogoutOutlined,
  BellOutlined,
  SettingOutlined,
  DashboardOutlined,
  TeamOutlined,
  DesktopOutlined,
  CodeOutlined,
  HomeOutlined
} from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector, logout, selectAuthUser, selectUserRole } from '../app/index';

const { Header: AntHeader } = Layout;
const { Text } = Typography;

/**
 * Header Component
 * 
 * Provides the main navigation header with:
 * - Application logo and brand
 * - Navigation links based on user role
 * - User profile information display
 * - Authentication controls (logout button)
 * - Responsive mobile menu
 * 
 * @component
 * @returns {React.ReactElement|null} The rendered Header component or null if user is not authenticated
 */
const Header = ({ collapsed, toggle }) => {
  const location = useLocation();
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  
  // Redux selectors
  const user = useAppSelector(selectAuthUser);
  const userRole = useAppSelector(selectUserRole);
  const [notifications] = useState([]);

  const handleLogout = async () => {
    try {
      await dispatch(logout()).unwrap();
      navigate('/login');
    } catch (error) {
      console.error('Logout failed:', error);
    }
  };

  const userMenu = (
    <Menu>
      <Menu.Item key="profile" icon={<UserOutlined />}>
        <Link to="/profile">Profile</Link>
      </Menu.Item>
      <Menu.Item key="settings" icon={<SettingOutlined />}>
        <Link to="/settings">Settings</Link>
      </Menu.Item>
      <Menu.Divider />
      <Menu.Item key="logout" icon={<LogoutOutlined />} onClick={handleLogout}>
        Logout
      </Menu.Item>
    </Menu>
  );

  const notificationMenu = (
    <Menu>
      {notifications.length > 0 ? (
        notifications.map((notification, index) => (
          <Menu.Item key={index}>
            <div className="notification-item">
              <Text strong>{notification.title}</Text>
              <Text type="secondary">{notification.message}</Text>
              <Text type="secondary" className="notification-time">
                {notification.time}
              </Text>
            </div>
          </Menu.Item>
        ))
      ) : (
        <Menu.Item disabled>No new notifications</Menu.Item>
      )}
    </Menu>
  );

  const getMenuItems = () => {
    const items = [
      {
        key: '/dashboard',
        icon: <DashboardOutlined />,
        label: <Link to="/dashboard">Dashboard</Link>,
      },
      {
        key: '/rooms',
        icon: <HomeOutlined />,
        label: <Link to="/rooms">Rooms</Link>,
      },
    ];

    if (userRole === 'admin') {
      items.push(
        {
          key: '/admin',
          icon: <SettingOutlined />,
          label: <Link to="/admin">Admin</Link>,
        },
        {
          key: '/admin/users',
          icon: <TeamOutlined />,
          label: <Link to="/admin/users">Users</Link>,
        },
        {
          key: '/admin/computers',
          icon: <DesktopOutlined />,
          label: <Link to="/admin/computers">Computers</Link>,
        },
        {
          key: '/admin/agent-versions',
          icon: <CodeOutlined />,
          label: <Link to="/admin/agent-versions">Agent Versions</Link>,
        }
      );
    }

    return items;
  };

  return (
    <AntHeader className="header">
      <div className="header-left">
        <Button
          type="text"
          icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
          onClick={toggle}
          className="trigger-button"
        />
        <Menu
          theme="dark"
          mode="horizontal"
          selectedKeys={[location.pathname]}
          items={getMenuItems()}
          className="header-menu"
        />
      </div>
      
      <div className="header-right">
        <Space size="large">
          <Dropdown overlay={notificationMenu} trigger={['click']} placement="bottomRight">
            <Badge count={notifications.length} size="small">
              <Button type="text" icon={<BellOutlined />} className="header-icon" />
            </Badge>
          </Dropdown>
          
          <Dropdown overlay={userMenu} trigger={['click']} placement="bottomRight">
            <Space className="user-dropdown">
              <Avatar icon={<UserOutlined />} />
              <span className="username">{user?.username}</span>
            </Space>
          </Dropdown>
        </Space>
      </div>
    </AntHeader>
  );
};

export default Header;
