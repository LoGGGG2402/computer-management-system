/**
 * @fileoverview Simple computer card component for compact computer display
 * 
 * This component provides a simplified view of a computer with essential status
 * information and real-time monitoring using WebSocket.
 * 
 * @module SimpleComputerCard
 */
import React, { useEffect, useState } from "react";
import {
  Card,
  Button,
  Tooltip,
  Badge,
  Typography,
  Row,
  Col,
  Progress,
  Popover,
  Tag,
} from "antd";
import {
  DesktopOutlined,
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  DatabaseOutlined,
  CodeOutlined,
  HddOutlined,
  RocketOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import { useSocket } from "../../contexts/SocketContext";
import { useCommandHandle } from "../../contexts/CommandHandleContext";
import { useNavigate } from "react-router-dom";
import { useFormatting } from "../../hooks/useFormatting";

const { Text } = Typography;

/**
 * Card style for computer cards
 * @constant
 */
export const cardStyle = {
  height: "180px",
  width: "100%",
  overflow: "hidden",
  borderRadius: "8px",
  boxShadow: "0 2px 8px rgba(0,0,0,0.1)",
  display: "flex",
  flexDirection: "column",
};

/**
 * SimpleComputerCard Component
 * 
 * Displays a compact view of a computer with real-time status monitoring.
 * Uses WebSockets to receive updates about the computer's status.
 *
 * @component
 * @param {Object} props - Component props
 * @param {Object} props.computer - Computer object with details to display
 * @returns {React.ReactElement} The rendered SimpleComputerCard component
 */
const SimpleComputerCard = ({ computer }) => {
  const {
    getComputerStatus,
    subscribeToComputer,
    unsubscribeFromComputer,
    isSocketReady,
  } = useSocket();
  const { commandResults, clearResult } = useCommandHandle();
  const navigate = useNavigate();
  const { formatRAMSize, formatDiskSize, getTimeAgo, getStatusColor } = useFormatting();

  const computerId = computer?.id;
  const [activeResultIndex, setActiveResultIndex] = useState(-1);
  const [popoverOpen, setPopoverOpen] = useState(false); // Renamed from popoverVisible

  const computerCommandResults = computerId
    ? commandResults[computerId] || []
    : [];
  const resultCount = computerCommandResults.length;

  // Set activeResultIndex to the last (most recent) result when command results change
  useEffect(() => {
    if (resultCount > 0 && activeResultIndex === -1) {
      setActiveResultIndex(resultCount - 1);
    } else if (resultCount > 0 && activeResultIndex >= resultCount) {
      setActiveResultIndex(resultCount - 1);
    }
  }, [resultCount, activeResultIndex]);

  const commandResult =
    activeResultIndex >= 0 ? computerCommandResults[activeResultIndex] : null;

  useEffect(() => {
    if (computerId && isSocketReady) {
      console.log(`[SimpleCard ${computerId}] Socket ready, subscribing.`);
      subscribeToComputer(computerId);
    } else {
      console.log(
        `[SimpleCard ${computerId}] Socket not ready or no ID, skipping subscription.`
      );
    }

    return () => {
      if (computerId && isSocketReady) {
        console.log(`[SimpleCard ${computerId}] Cleaning up, unsubscribing.`);
        unsubscribeFromComputer(computerId);
      }
    };
  }, [computerId, subscribeToComputer, unsubscribeFromComputer, isSocketReady]);

  const statusData = getComputerStatus(computerId);

  const handleDoubleClick = () => {
    if (computerId) navigate(`/computers/${computerId}`);
  };

  const navigateToConsole = (e) => {
    e.stopPropagation();
    setPopoverOpen(false); // Updated state setter
    navigate(`/computers/${computerId}?tab=console`);
  };

  const handlePopoverOpenChange = (open) => { // Renamed from handlePopoverVisibleChange
    setPopoverOpen(open); // Updated state setter
  };

  if (!computer) return null;

  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return "Never";
    return getTimeAgo(computer.last_update);
  };

  const isOnline =
    (isSocketReady && statusData?.status === "online") ||
    (!isSocketReady && computer.status === "online") ||
    (computer.last_update &&
      new Date() - new Date(computer.last_update) < 5 * 60 * 1000);

  const cpuUsage = statusData?.cpuUsage ?? 0;
  const ramUsage = statusData?.ramUsage ?? 0;
  const diskUsage = statusData?.diskUsage ?? computer?.disk_usage ?? 0;

  const renderCommandResultContent = () => {
    if (!commandResult) return null;

    // Extract result data based on command type
    const resultData = commandResult.result || {};
    const stdout =
      commandResult.type === "console"
        ? resultData.stdout || ""
        : commandResult.stdout || "";
    const stderr =
      commandResult.type === "console"
        ? resultData.stderr || ""
        : commandResult.stderr || "";
    const exitCode =
      commandResult.type === "console"
        ? resultData.exitCode
        : commandResult.exitCode;
    const commandType = commandResult.type || "unknown";

    return (
      <div
        style={{ maxWidth: "300px", cursor: "pointer" }}
        onClick={navigateToConsole}
      >
        <div style={{ marginBottom: "8px" }}>
          <Text strong>
            Command Result {activeResultIndex + 1} of {resultCount}
          </Text>
          <Button
            size="small"
            type="text"
            style={{ float: "right", padding: "0" }}
            onClick={(e) => {
              e.stopPropagation();
              clearResult(computerId, activeResultIndex);
            }}
          >
            Clear
          </Button>
        </div>

        {/* Display command type */}
        <div style={{ marginBottom: "8px" }}>
          <Tag color="blue">{commandType}</Tag>
        </div>

        {/* Display the command that was executed */}
        <div style={{ marginBottom: "8px" }}>
          <Text code style={{ display: "block", wordBreak: "break-all" }}>
            $ {commandResult.commandText || "[Unknown Command]"}
          </Text>
        </div>

        <div
          style={{
            marginBottom: "8px",
            display: "flex",
            justifyContent: "space-between",
          }}
        >
          <Tag color={exitCode === 0 ? "success" : "error"}>
            Exit Code: {exitCode !== undefined ? exitCode : "N/A"}
          </Tag>
          <Text type="secondary" style={{ fontSize: "12px" }}>
            {getTimeAgo(commandResult.timestamp)}
          </Text>
        </div>

        {resultCount > 1 && (
          <div
            style={{
              marginBottom: "10px",
              display: "flex",
              justifyContent: "center",
              gap: "8px",
            }}
          >
            <Button
              size="small"
              disabled={activeResultIndex === 0}
              onClick={(e) => {
                e.stopPropagation();
                setActiveResultIndex((prev) => prev - 1);
              }}
            >
              Previous
            </Button>
            <Button
              size="small"
              disabled={activeResultIndex === resultCount - 1}
              onClick={(e) => {
                e.stopPropagation();
                setActiveResultIndex((prev) => prev + 1);
              }}
            >
              Next
            </Button>
          </div>
        )}

        {stdout && (
          <div style={{ marginBottom: "8px" }}>
            <Text strong>Output:</Text>
            <div
              style={{
                background: "#f5f5f5",
                padding: "8px",
                borderRadius: "4px",
                maxHeight: "150px",
                overflowY: "auto",
                whiteSpace: "pre-wrap",
                wordBreak: "break-all",
                fontSize: "12px",
              }}
            >
              {stdout}
            </div>
          </div>
        )}

        {stderr && (
          <div>
            <Text strong type="danger">
              Error:
            </Text>
            <div
              style={{
                background: "#fff2f0",
                padding: "8px",
                borderRadius: "4px",
                maxHeight: "150px",
                overflowY: "auto",
                whiteSpace: "pre-wrap",
                wordBreak: "break-all",
                fontSize: "12px",
                color: "#ff4d4f",
              }}
            >
              {stderr}
            </div>
          </div>
        )}
      </div>
    );
  };

  return (
    <Card
      hoverable
      size="small"
      style={{
        ...cardStyle,
        border: isOnline ? "1px solid #52c41a" : "1px solid #d9d9d9",
        height: "180px",
        width: "100%",
        maxWidth: "100%",
      }}
      title={
        <div
          style={{
            display: "flex",
            alignItems: "center",
            fontSize: "12px",
            width: "100%",
          }}
        >
          <Badge
            status={isOnline ? "success" : "default"}
            style={{ fontSize: "10px" }}
          />
          <span
            style={{
              marginLeft: "4px",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
              fontSize: "12px",
              flex: 1,
            }}
          >
            {computer.name}
          </span>

          {computer.have_active_errors && (
            <>
              <Tooltip title="Computer has active errors">
                <ExclamationCircleOutlined
                  style={{
                    color: "#ff4d4f",
                    fontSize: "14px",
                    marginLeft: "4px",
                    cursor: "pointer",
                  }}
                  onClick={(e) => {
                    e.stopPropagation();
                    navigate(`/computers/${computerId}?tab=errors`);
                  }}
                />
              </Tooltip>
            </>
          )}

          {resultCount > 0 && (
            <Popover
              content={renderCommandResultContent()}
              title={null}
              trigger="hover"
              placement="right"
              overlayStyle={{ width: "300px" }}
              open={popoverOpen} // Changed from visible
              onOpenChange={handlePopoverOpenChange} // Changed from onVisibleChange
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  marginLeft: "4px",
                  cursor: "pointer",
                }}
              >
                <CodeOutlined style={{ color: "#1890ff", fontSize: "14px" }} />
                {resultCount > 1 && (
                  <span
                    style={{
                      fontSize: "10px",
                      marginLeft: "2px",
                      backgroundColor: "#1890ff",
                      color: "white",
                      borderRadius: "8px",
                      padding: "0 4px",
                      minWidth: "16px",
                      height: "16px",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    {resultCount}
                  </span>
                )}
              </div>
            </Popover>
          )}
        </div>
      }
      className="computer-card-simple"
      extra={
        <div style={{ display: "flex", flexShrink: 0 }}>
          <Tooltip title="View details">
            <Button
              type="text"
              icon={<DesktopOutlined style={{ fontSize: "12px" }} />}
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                navigate(`/computers/${computerId}`);
              }}
            />
          </Tooltip>
        </div>
      }
      styles={{
        body: {
          flex: 1,
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          padding: "8px",
          width: "100%",
          overflow: "hidden",
        },
        header: {
          padding: "0 12px",
          minHeight: "32px",
          fontSize: "12px",
          width: "100%",
          display: "flex",
          alignItems: "center",
          overflow: "hidden",
        },
      }}
      onDoubleClick={handleDoubleClick}
      onClick={null}
    >
      <div style={{ overflow: "hidden", width: "100%" }}>
        <Row gutter={[4, 4]}>
          <Col span={12}>
            <Tooltip title="IP Address">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <GlobalOutlined
                  style={{ marginRight: "4px", fontSize: "11px" }}
                />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {computer.ip_address || "No IP"}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="Last seen">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <ClockCircleOutlined
                  style={{ marginRight: "4px", fontSize: "11px" }}
                />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {getTimeSinceLastSeen()}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="CPU Info">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <InfoCircleOutlined
                  style={{ marginRight: "4px", fontSize: "11px" }}
                />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {computer.cpu_info
                    ? computer.cpu_info.split(" ").slice(0, 2).join(" ")
                    : "No CPU info"}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="RAM">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <DatabaseOutlined
                  style={{ marginRight: "4px", fontSize: "11px" }}
                />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {formatRAMSize(computer.total_ram)}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="Disk Space">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <HddOutlined style={{ marginRight: "4px", fontSize: "11px" }} />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {formatDiskSize(computer.total_disk_space)}
                </span>
              </p>
            </Tooltip>
          </Col>
          <Col span={12}>
            <Tooltip title="GPU">
              <p
                style={{
                  margin: "2px 0",
                  display: "flex",
                  alignItems: "center",
                  fontSize: "11px",
                }}
              >
                <RocketOutlined
                  style={{ marginRight: "4px", fontSize: "11px" }}
                />
                <span
                  style={{
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    fontSize: "11px",
                  }}
                >
                  {computer.gpu_info || "No GPU info"}
                </span>
              </p>
            </Tooltip>
          </Col>
          {computer.room?.name && (
            <Col span={12}>
              <Tooltip title="Room">
                <p
                  style={{
                    margin: "2px 0",
                    display: "flex",
                    alignItems: "center",
                    fontSize: "11px",
                  }}
                >
                  <HomeOutlined
                    style={{ marginRight: "4px", fontSize: "11px" }}
                  />
                  <span
                    style={{
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                      fontSize: "11px",
                    }}
                  >
                    {computer.room.name}
                  </span>
                </p>
              </Tooltip>
            </Col>
          )}
        </Row>
      </div>

      <Row
        gutter={[4, 0]}
        style={{ marginTop: "auto", paddingTop: "4px", width: "100%" }}
      >
        <Col span={8}>
          <Tooltip title={`CPU: ${cpuUsage}%`}>
            <Progress
              percent={cpuUsage}
              size="small"
              showInfo={false}
              strokeColor={getStatusColor(cpuUsage)}
            />
            <div style={{ textAlign: "center", fontSize: "9px" }}>CPU</div>
          </Tooltip>
        </Col>
        <Col span={8}>
          <Tooltip title={`RAM: ${ramUsage}%`}>
            <Progress
              percent={ramUsage}
              size="small"
              showInfo={false}
              strokeColor={getStatusColor(ramUsage)}
            />
            <div style={{ textAlign: "center", fontSize: "9px" }}>RAM</div>
          </Tooltip>
        </Col>
        <Col span={8}>
          <Tooltip title={`Disk: ${diskUsage}%`}>
            <Progress
              percent={diskUsage}
              size="small"
              showInfo={false}
              strokeColor={getStatusColor(diskUsage)}
            />
            <div style={{ textAlign: "center", fontSize: "9px" }}>Disk</div>
          </Tooltip>
        </Col>
      </Row>
    </Card>
  );
};

export default SimpleComputerCard;
