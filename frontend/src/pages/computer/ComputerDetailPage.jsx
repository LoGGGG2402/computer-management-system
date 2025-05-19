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
  Tag,
  Space,
  Card,
} from "antd";
import {
  ArrowLeftOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  CodeOutlined,
} from "@ant-design/icons";
import computerService from "../../services/computer.service";
import { useSocket } from "../../contexts/SocketContext";
import ComputerCard from "../../components/computer/ComputerCard";
import ComputerError from "../../components/computer/ComputerError";
import ComputerConsole from "../../components/computer/ComputerConsole";
import { LoadingComponent } from "../../components/common";
import { useSimpleFetch } from "../../hooks/useSimpleFetch";

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
  const { subscribeToComputer, unsubscribeFromComputer, getComputerStatus, isSocketReady } = useSocket();

  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const initialTab = searchParams.get('tab') || 'overview';
  const [activeTab, setActiveTab] = useState(initialTab);

  const computerIdInt = useMemo(() => parseInt(id), [id]);

  // Fetch computer details using useSimpleFetch
  const fetchComputerDetailsCallback = useCallback(() => {
    if (!computerIdInt) return Promise.resolve(null);
    return computerService.getComputerById(computerIdInt);
  }, [computerIdInt]);

  const { data: computer, loading, error: fetchError, refresh: refreshComputerDetails } = useSimpleFetch(
    fetchComputerDetailsCallback,
    [fetchComputerDetailsCallback],
    { errorMessage: "Failed to load computer details" }
  );

  const computerStatus = getComputerStatus(computerIdInt);
  const isOnline = useMemo(() => {
    return (isSocketReady && computerStatus?.status === "online") ||
      (!isSocketReady && computer?.status === "online") ||
      (computer?.last_update && new Date() - new Date(computer.last_update) < 5 * 60 * 1000);
  }, [isSocketReady, computerStatus, computer]);

  /**
   * Handles WebSocket subscription for real-time computer status
   * 
   * @effect
   * @dependency {number} computerIdInt - Parsed integer computer ID
   * @dependency {boolean} isSocketReady - WebSocket connection status
   */
  useEffect(() => {
    if (computerIdInt && isSocketReady) {
      subscribeToComputer(computerIdInt);
    }

    return () => {
      if (computerIdInt && isSocketReady) {
        unsubscribeFromComputer(computerIdInt);
      }
    };
  }, [computerIdInt, subscribeToComputer, unsubscribeFromComputer, isSocketReady]);

  /**
   * Triggers a refresh of computer details
   * 
   * @function
   */
  const handleRefresh = () => {
    // refreshComputerDetails();
  };

  /**
   * Handles navigation to previous page
   * 
   * @function
   */
  const handleGoBack = () => {
    navigate(-1);
  };

  /**
   * Handles tab change and updates URL query parameter
   * 
   * @function
   * @param {string} key - Selected tab key
   */
  const handleTabChange = (key) => {
    setActiveTab(key);
    const newSearchParams = new URLSearchParams(location.search);
    newSearchParams.set('tab', key);
    navigate(`${location.pathname}?${newSearchParams.toString()}`, { replace: true });
  };
  
  /**
   * Defines tab items for the tabbed interface
   * 
   * @type {Array<Object>}
   */
  const items = useMemo(() => [
    {
      key: "overview",
      label: "Overview",
      children: (
        <div className="computer-overview">
          {computer && (
            <ComputerCard
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
      key: "errors",
      label: (
        <span>
          <ExclamationCircleOutlined /> Errors
        </span>
      ),
      children: (
        <ComputerError
          computerId={computerIdInt}
          onRefresh={handleRefresh}
        />
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

  if (fetchError || !computer) {
    return (
      <Card className="computer-detail-page">
        <div className="text-center">
          <Title level={4}>{fetchError || "Computer not found"}</Title>
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
