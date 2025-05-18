/**
 * @fileoverview Main layout component with notification handling
 *
 * This component defines the main layout structure for the application,
 * including the header, content area, footer, and system notification handler.
 *
 * @module MainLayout
 */
import React, { useEffect, useState } from "react";
import { Outlet, useNavigate, useLocation } from "react-router-dom";
import { Toaster, toast } from "react-hot-toast";
import { Layout, Menu } from "antd";
import {
  DashboardOutlined,
  HomeOutlined,
  SettingOutlined,
  TeamOutlined,
  DesktopOutlined,
  CodeOutlined,
} from "@ant-design/icons";
import Header from "./Header";
import { useAppSelector, selectUserRole, selectPendingAgentMFA, selectRegisteredAgents } from "../app/index";

const { Content, Sider } = Layout;

/**
 * NotificationHandler Component
 *
 * Manages real-time system notifications using Redux state
 * for agent MFA verification and registration alerts.
 *
 * @component
 * @returns {null} - This component doesn't render any UI elements directly
 */
const NotificationHandler = () => {
  const userRole = useAppSelector(selectUserRole);
  const isAdmin = userRole === 'admin';
  const navigate = useNavigate();
  const pendingMFA = useAppSelector(selectPendingAgentMFA);
  const registeredAgents = useAppSelector(selectRegisteredAgents);

  /**
   * Handles MFA notifications from Redux state
   */
  useEffect(() => {
    if (!isAdmin || !pendingMFA) {
      return;
    }

    const positionInfoText = pendingMFA.positionInfo
      ? `Location: ${
          pendingMFA.positionInfo.roomName || "Unknown"
        }\nPosition: Row ${pendingMFA.positionInfo.posY || "?"}, Column ${
          pendingMFA.positionInfo.posX || "?"
        }`
      : null;

    toast(
      (t) => (
        <div
          onClick={() => {
            toast.dismiss(t.id);
            toast.success("MFA code copied to clipboard!");
          }}
          className="cursor-pointer p-1"
        >
          <div className="font-bold text-blue-600">New Agent MFA</div>
          <div>Agent requires MFA verification with code:</div>
          <div className="bg-gray-100 px-2 py-1 rounded my-1 font-mono">
            {pendingMFA.mfaCode}
          </div>
          {positionInfoText && (
            <div className="text-sm text-gray-600 mt-1">
              {positionInfoText.split("\n").map((line, i) => (
                <div key={i}>{line}</div>
              ))}
            </div>
          )}
          <div className="text-xs text-gray-500 mt-1">Click to copy code</div>
        </div>
      ),
      {
        duration: 10000,
        style: {
          padding: "16px",
          borderRadius: "8px",
          background: "#fff",
          boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
          border: "1px solid #e2e8f0",
        },
      }
    );
  }, [isAdmin, pendingMFA]);

  /**
   * Handles agent registration notifications from Redux state
   */
  useEffect(() => {
    if (!isAdmin || !registeredAgents.length) {
      return;
    }

    // Chỉ hiển thị thông báo cho agent mới nhất
    const latestAgent = registeredAgents[0];
    const positionInfoText = latestAgent.positionInfo
      ? `Location: ${
          latestAgent.positionInfo.roomName || "Unknown"
        }\nPosition: Row ${latestAgent.positionInfo.posY || "?"}, Column ${
          latestAgent.positionInfo.posX || "?"
        }`
      : null;

    toast.success(
      (t) => (
        <div
          onClick={() => {
            toast.dismiss(t.id);
            navigate(`/computers/${latestAgent.computerId}`);
          }}
          className="cursor-pointer"
        >
          <div className="font-bold">New Agent Registered</div>
          <div>A new agent has been registered.</div>
          <div className="bg-gray-100 px-2 py-1 rounded my-1 font-mono">
            {latestAgent.computerId}
          </div>
          {positionInfoText && (
            <div className="text-sm text-gray-600 mt-1">
              {positionInfoText.split("\n").map((line, i) => (
                <div key={i}>{line}</div>
              ))}
            </div>
          )}
          <div className="text-xs text-gray-500 mt-1">
            Click to view computer details
          </div>
        </div>
      ),
      {
        duration: 8000,
        style: {
          padding: "16px",
          cursor: "pointer",
        },
      }
    );
  }, [isAdmin, registeredAgents, navigate]);

  return null;
};

/**
 * MainLayout Component
 *
 * Provides the overall layout structure for the application with:
 * - Header for navigation
 * - Main content area that renders child routes
 * - Footer with copyright information
 * - Notification system for real-time alerts
 *
 * @component
 * @returns {React.ReactElement} The rendered MainLayout component
 */
const MainLayout = () => {
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();
  const userRole = useAppSelector(selectUserRole);

  const toggle = () => {
    setCollapsed(!collapsed);
  };

  const getMenuItems = () => {
    const items = [
      {
        key: "/dashboard",
        icon: <DashboardOutlined />,
        label: "Dashboard",
      },
      {
        key: "/rooms",
        icon: <HomeOutlined />,
        label: "Rooms",
      },
    ];

    if (userRole === "admin") {
      items.push(
        {
          key: "/admin",
          icon: <SettingOutlined />,
          label: "Admin",
        },
        {
          key: "/admin/users",
          icon: <TeamOutlined />,
          label: "Users",
        },
        {
          key: "/admin/computers",
          icon: <DesktopOutlined />,
          label: "Computers",
        },
        {
          key: "/admin/agent-versions",
          icon: <CodeOutlined />,
          label: "Agent Versions",
        }
      );
    }

    return items;
  };

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Header collapsed={collapsed} toggle={toggle} />
      <Layout>
        <Sider
          trigger={null}
          collapsible
          collapsed={collapsed}
          width={200}
          style={{
            overflow: "auto",
            height: "100vh",
            position: "fixed",
            left: 0,
            top: 64,
          }}
        >
          <Menu
            theme="dark"
            mode="inline"
            selectedKeys={[location.pathname]}
            items={getMenuItems()}
          />
        </Sider>
        <Layout
          style={{ marginLeft: collapsed ? 80 : 200, transition: "all 0.2s" }}
        >
          <Content style={{ margin: "24px 16px", padding: 24, minHeight: 280 }}>
            <Outlet />
          </Content>
        </Layout>
      </Layout>
      <NotificationHandler />
      <Toaster position="top-right" />
    </Layout>
  );
};

export default MainLayout;
