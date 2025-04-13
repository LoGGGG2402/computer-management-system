import React, { useState, useEffect } from "react";
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
} from "@ant-design/icons";
import computerService from "../../services/computer.service";
import { useSocket } from "../../contexts/SocketContext";
import ComputerCard from "../../components/computer/ComputerCard";
import { LoadingComponent } from "../../components/common";

const { Title, Text } = Typography;

const ComputerDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { subscribeToComputer, unsubscribeFromComputer, getComputerStatus } = useSocket();

  const [computer, setComputer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("overview");
  const [refreshKey, setRefreshKey] = useState(0);

  // Get real-time status from socket context
  const computerStatus = getComputerStatus(parseInt(id));
  const isOnline = computerStatus?.status === "online";

  // Load computer details when component mounts or refreshKey changes
  useEffect(() => {
    fetchComputerDetails();
  }, [id, refreshKey]);

  // Subscribe to computer updates when id is available
  useEffect(() => {
    if (id) {
      subscribeToComputer(parseInt(id));
    }

    // Clean up by unsubscribing when component unmounts
    return () => {
      if (id) {
        unsubscribeFromComputer(parseInt(id));
      }
    };
  }, [id, subscribeToComputer, unsubscribeFromComputer]);

  const fetchComputerDetails = async () => {
    setLoading(true);
    try {
      const data = await computerService.getComputerById(parseInt(id));
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
    switch (status) {
      case "online":
        return (
          <Tag color="success" icon={<CheckCircleOutlined />}>
            Online
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

  // Define tab items
  const items = [
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
  ];

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
