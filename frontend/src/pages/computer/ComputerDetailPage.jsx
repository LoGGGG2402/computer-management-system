import React, { useState, useEffect, useMemo } from "react"; // Import useMemo
import { useParams, useNavigate } from "react-router-dom";
import {
  Button,
  message,
  Typography,
  Breadcrumb,
  Tag,
  Space,
  Alert,
  Tabs,
} from "antd";
import {
  HomeOutlined,
  DesktopOutlined,
  ArrowLeftOutlined,
  ReloadOutlined,
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

const { Title, Text } = Typography;

const ComputerDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { subscribeToComputer, unsubscribeFromComputer, getComputerStatus, isSocketReady } = useSocket();

  const [computer, setComputer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("overview");
  const [refreshKey, setRefreshKey] = useState(0);

  const computerIdInt = useMemo(() => parseInt(id), [id]); // Memoize parsed ID

  // Get real-time status from socket context
  const computerStatus = getComputerStatus(computerIdInt);
  const isOnline = isSocketReady && computerStatus && computerStatus.status === "online";

  // Load computer details when component mounts or refreshKey changes
  useEffect(() => {
    fetchComputerDetails();
  }, [id, refreshKey]);

  // Subscribe to computer updates when id is available AND socket is ready
  useEffect(() => {
    if (computerIdInt && isSocketReady) {
      console.log(`[DetailPage ${computerIdInt}] Socket ready, subscribing.`);
      subscribeToComputer(computerIdInt);
    } else {
      console.log(`[DetailPage ${id}] Socket not ready or no ID, skipping subscription.`);
    }

    // Clean up by unsubscribing when component unmounts or dependencies change
    return () => {
      if (computerIdInt && isSocketReady) {
        console.log(`[DetailPage ${computerIdInt}] Cleaning up, unsubscribing.`);
        unsubscribeFromComputer(computerIdInt);
      }
    };
  }, [id, subscribeToComputer, unsubscribeFromComputer, isSocketReady, computerIdInt]);

  const fetchComputerDetails = async () => {
    setLoading(true);
    try {
      const data = await computerService.getComputerById(computerIdInt);
      setComputer(data);
      setError(null);
    } catch (error) {
      console.error("Error fetching computer details:", error);
      setError("Failed to load computer details. Please try again later.");
      message.error("Failed to load computer details");
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = () => {
    setRefreshKey((prev) => prev + 1);
  };

  const handleGoBack = () => {
    navigate(-1);
  };

  // Render status tag
  const renderStatusTag = (status) => {
    const displayStatus = isSocketReady ? status : (computer?.status || 'unknown');
    switch (displayStatus) {
      case "online":
        return (
          <Tag color="success" icon={<CheckCircleOutlined />}>
            Online { !isSocketReady && computer?.status === 'online' ? '(cached)' : ''}
          </Tag>
        );
      case "offline":
        return (
          <Tag color="error" icon={<CloseCircleOutlined />}>
            Offline
          </Tag>
        );
      default:
        return <Tag color="default">Unknown</Tag>;
    }
  };

  // Define tab items using useMemo
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
              diskUsage={computerStatus?.diskUsage || 0}
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

  const handleTabChange = (key) => {
    setActiveTab(key);
  };

  if (loading) {
    return (
      <LoadingComponent
        tip="Đang tải thông tin máy tính..."
        type="section"
        size="large"
      />
    );
  }

  if (error) {
    return (
      <div className="not-found">
        <Alert message="Error" description={error} type="error" showIcon />
        <Button
          type="primary"
          onClick={handleGoBack}
          style={{ marginTop: "16px" }}
        >
          <ArrowLeftOutlined /> Go Back
        </Button>
      </div>
    );
  }

  if (!computer) {
    return (
      <div className="not-found">
        <Alert
          message="Computer Not Found"
          description="The requested computer could not be found."
          type="warning"
          showIcon
        />
        <Button
          type="primary"
          onClick={handleGoBack}
          style={{ marginTop: "16px" }}
        >
          <ArrowLeftOutlined /> Go Back
        </Button>
      </div>
    );
  }

  return (
    <div className="computer-detail-page">
      <div className="page-header" style={{ marginBottom: "24px" }}>
        <Breadcrumb
          items={[
            {
              title: (
                <span className="cursor-pointer text-blue-600 hover:text-blue-800 hover:underline transition-colors duration-200 ease-in-out">
                  <HomeOutlined /> Home
                </span>
              ),
              onClick: () => navigate("/dashboard"),
            },
            {
              title: (
                <span className="cursor-pointer text-blue-600 hover:text-blue-800 hover:underline transition-colors duration-200 ease-in-out">
                  Rooms
                </span>
              ),
              onClick: () => navigate("/rooms"),
            },
            {
              title: (
                <span className="cursor-pointer text-blue-600 hover:text-blue-800 hover:underline transition-colors duration-200 ease-in-out">
                  {computer.room?.name || "Room"}
                </span>
              ),
              onClick: () => computer.room_id && navigate(`/rooms/${computer.room_id}`),
            },
            {
              title: (
                <span className="text-gray-500 font-medium">
                  {computer.name || "Computer"}
                </span>
              ),
            },
          ]}
        />

        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginTop: "16px",
          }}
        >
          <div>
            <Title level={2}>
              <DesktopOutlined /> {computer.name}
            </Title>
            <Space>
              {renderStatusTag(isOnline ? "online" : "offline")}
              <Text type="secondary">
                ID: {computer.id} &bull; Room:{" "}
                {computer.room?.name || "Unknown"}
              </Text>
            </Space>
          </div>
          <div>
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleGoBack}
              style={{ marginRight: "8px" }}
            >
              Back
            </Button>
            <Button
              type="primary"
              icon={<ReloadOutlined />}
              onClick={handleRefresh}
            >
              Refresh
            </Button>
          </div>
        </div>
      </div>

      <Tabs
        activeKey={activeTab}
        items={items}
        onChange={handleTabChange}
        tabBarStyle={{ marginBottom: "16px" }}
      />
    </div>
  );
};

export default ComputerDetailPage;
