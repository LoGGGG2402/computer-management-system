/**
 * @fileoverview Detailed computer view component for the Computer Management System
 * 
 * This component displays comprehensive information about a single computer,
 * including its status, hardware information, error logs, and console access.
 * 
 * @module ComputerDetailPage
 */
import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import {
  Button,
  Typography,
  Card,
  Space,
} from "antd";
import {
  ArrowLeftOutlined,
  CodeOutlined,
} from "@ant-design/icons";
import ComputerDetail from "../../components/computer/ComputerDetail";
import ComputerConsole from "../../components/computer/ComputerConsole";
import { LoadingComponent } from "../../components/common";
import {
  useAppDispatch,
  useAppSelector,
  fetchComputerById,
  selectSelectedComputer,
  selectComputerLoading,
  selectComputerError,
  selectComputerStatus,
  selectSocketConnected
} from "../../app/index";

const { Title } = Typography;

/**
 * Computer Detail Page Component
 * 
 * Displays detailed information about a specific computer with:
 * - Basic information and online status
 * - System resource usage (CPU, RAM, disk)
 * - Error logs and management
 * - Real-time console access
 * - Tab-based navigation between different sections
 * 
 * @component
 * @returns {React.ReactElement} The rendered ComputerDetailPage component
 */
const ComputerDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const dispatch = useAppDispatch();

  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const initialTab = searchParams.get('tab') || 'overview';
  const [activeTab, setActiveTab] = useState(initialTab);

  const computerIdInt = useMemo(() => parseInt(id), [id]);

  // Redux selectors
  const computer = useAppSelector(selectSelectedComputer);
  const loading = useAppSelector(selectComputerLoading);
  const error = useAppSelector(selectComputerError);
  const computerStatus = useAppSelector(state => selectComputerStatus(state, computerIdInt));
  const isSocketConnected = useAppSelector(selectSocketConnected);

  const isOnline = useMemo(() => {
    return (isSocketConnected && computerStatus?.status === "online") ||
      (!isSocketConnected && computer?.status === "online") ||
      (computer?.last_update && new Date() - new Date(computer.last_update) < 5 * 60 * 1000);
  }, [isSocketConnected, computerStatus, computer]);

  /**
   * Fetches computer details when ID changes
   */
  useEffect(() => {
    if (computerIdInt) {
      dispatch(fetchComputerById(computerIdInt));
    }
  }, [dispatch, computerIdInt]);

  /**
   * Triggers a refresh of computer details
   */
  const handleRefresh = useCallback(() => {
    if (computerIdInt) {
      dispatch(fetchComputerById(computerIdInt));
    }
  }, [dispatch, computerIdInt]);

  /**
   * Handles navigation to previous page
   */
  const handleGoBack = () => {
    navigate(-1);
  };

  /**
   * Handles tab change and updates URL query parameter
   */
  const handleTabChange = (key) => {
    setActiveTab(key);
    const newSearchParams = new URLSearchParams(location.search);
    newSearchParams.set('tab', key);
    navigate(`${location.pathname}?${newSearchParams.toString()}`, { replace: true });
  };
  
  /**
   * Defines tab items for the tabbed interface
   */
  const items = useMemo(() => [
    {
      key: "overview",
      label: "Overview",
      children: (
        <div className="computer-overview">
          {computer && (
            <ComputerDetail
              computer={computer}
              isOnline={isOnline}
              cpuUsage={computerStatus?.cpuUsage || 0}
              ramUsage={computerStatus?.ramUsage || 0}
              diskUsage={computerStatus?.diskUsage || computer?.disk_usage || 0}
              onRefresh={handleRefresh}
            />
          )}
        </div>
      ),
    },
    {
      key: "console",
      label: (
        <span>
          <CodeOutlined /> Console
        </span>
      ),
      children: (
        <ComputerConsole
          key={computerIdInt}
          computerId={computerIdInt}
          computer={computer}
          isOnline={isOnline}
        />
      ),
    },
  ], [computerIdInt, computer, isOnline, computerStatus, handleRefresh]);

  if (loading) {
    return (
      <LoadingComponent
        tip="Loading computer details..."
        type="section"
        size="large"
      />
    );
  }

  if (error || !computer) {
    return (
      <Card className="computer-detail-page">
        <div className="text-center">
          <Title level={4}>{error || "Computer not found"}</Title>
          <Button
            type="primary"
            icon={<ArrowLeftOutlined />}
            onClick={handleGoBack}
          >
            Go Back
          </Button>
        </div>
      </Card>
    );
  }

  return (
    <div className="computer-detail-page">
      <Card
        title={
          <Space>
            <Button icon={<ArrowLeftOutlined />} onClick={handleGoBack}>
              Back
            </Button>
            <span>{computer.name}</span>
          </Space>
        }
        extra={
          <Button onClick={handleRefresh} disabled={loading}>
            Refresh
          </Button>
        }
        tabList={items.map(item => ({ key: item.key, tab: item.label }))}
        activeTabKey={activeTab}
        onTabChange={handleTabChange}
      >
        {items.find(item => item.key === activeTab)?.children}
      </Card>
    </div>
  );
};

export default ComputerDetailPage;
