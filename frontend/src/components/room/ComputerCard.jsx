/**
 * @fileoverview Simple computer card component for compact computer display
 * 
 * This component provides a simplified view of a computer with essential status
 * information and real-time monitoring using WebSocket.
 * 
 * @module ComputerCard
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
  ClockCircleOutlined,
  DatabaseOutlined,
  HddOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { useFormatting } from "../../app/index";
import {
  useAppDispatch,
  useAppSelector,
  selectSocketConnected,
  selectComputerStatus,
  selectCommandHistory,
  clearCommandHistory
} from "../../app/index";

const { Text } = Typography;

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
const ComputerCard = ({ computer }) => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { formatRAMSize, formatDiskSize, getTimeAgo, getStatusColor } = useFormatting();

  const computerId = computer?.id;
  const [activeResultIndex, setActiveResultIndex] = useState(-1);
  const [popoverOpen, setPopoverOpen] = useState(false);

  // Redux state
  const isSocketReady = useAppSelector(selectSocketConnected);
  const statusData = useAppSelector(state => selectComputerStatus(state, computerId));
  const commandHistory = useAppSelector(selectCommandHistory);

  const computerCommandResults = computerId ? commandHistory[computerId] || [] : [];
  const resultCount = computerCommandResults.length;

  useEffect(() => {
    if (resultCount > 0 && activeResultIndex === -1) {
      setActiveResultIndex(resultCount - 1);
    } else if (resultCount > 0 && activeResultIndex >= resultCount) {
      setActiveResultIndex(resultCount - 1);
    }
  }, [resultCount, activeResultIndex]);

  const commandResult = activeResultIndex >= 0 ? computerCommandResults[activeResultIndex] : null;

  const handleDoubleClick = () => {
    if (computerId) navigate(`/computers/${computerId}`);
  };

  const navigateToConsole = (e) => {
    e.stopPropagation();
    setPopoverOpen(false);
    navigate(`/computers/${computerId}?tab=console`);
  };

  const handlePopoverOpenChange = (open) => {
    setPopoverOpen(open);
  };

  if (!computer) return null;

  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return "Never";
    return getTimeAgo(computer.last_update);
  };

  const isOnline = (isSocketReady && statusData?.status === "online");

  const cpuUsage = statusData?.cpuUsage ?? 0;
  const ramUsage = statusData?.ramUsage ?? 0;
  const diskUsage = statusData?.diskUsage ?? computer?.disk_usage ?? 0;

  const renderCommandResultContent = () => {
    if (!commandResult) return null;

    const resultData = commandResult.result || {};
    const stdout = commandResult.type === "console" ? resultData.stdout || "" : commandResult.stdout || "";
    const stderr = commandResult.type === "console" ? resultData.stderr || "" : commandResult.stderr || "";
    const exitCode = commandResult.type === "console" ? resultData.exitCode : commandResult.exitCode;
    const commandType = commandResult.type || "unknown";

    return (
      <div style={{ maxWidth: "300px", cursor: "pointer" }} onClick={navigateToConsole}>
        <div style={{ marginBottom: "8px" }}>
          <Text strong>Command Result {activeResultIndex + 1} of {resultCount}</Text>
          <Button
            size="small"
            type="text"
            style={{ float: "right", padding: "0" }}
            onClick={(e) => {
              e.stopPropagation();
              dispatch(clearCommandHistory({ computerId, index: activeResultIndex }));
            }}
          >
            Clear
          </Button>
        </div>

        <div style={{ marginBottom: "8px" }}>
          <Tag color="blue">{commandType}</Tag>
        </div>

        <div style={{ marginBottom: "8px" }}>
          <Text code style={{ display: "block", wordBreak: "break-all" }}>
            $ {commandResult.commandText || "[Unknown Command]"}
          </Text>
        </div>

        <div style={{ marginBottom: "8px", display: "flex", justifyContent: "space-between" }}>
          <Tag color={exitCode === 0 ? "success" : "error"}>
            Exit Code: {exitCode !== undefined ? exitCode : "N/A"}
          </Tag>
          <Text type="secondary" style={{ fontSize: "12px" }}>
            {getTimeAgo(commandResult.timestamp)}
          </Text>
        </div>

        {resultCount > 1 && (
          <div style={{ marginBottom: "10px", display: "flex", justifyContent: "center", gap: "8px" }}>
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
            <div style={{ maxHeight: "100px", overflow: "auto", backgroundColor: "#f5f5f5", padding: "4px", borderRadius: "4px" }}>
              <Text style={{ whiteSpace: "pre-wrap", wordBreak: "break-all" }}>{stdout}</Text>
            </div>
          </div>
        )}

        {stderr && (
          <div style={{ marginBottom: "8px" }}>
            <Text strong type="danger">Error:</Text>
            <div style={{ maxHeight: "100px", overflow: "auto", backgroundColor: "#fff2f0", padding: "4px", borderRadius: "4px" }}>
              <Text type="danger" style={{ whiteSpace: "pre-wrap", wordBreak: "break-all" }}>{stderr}</Text>
            </div>
          </div>
        )}
      </div>
    );
  };

  return (
    <Popover
      content={renderCommandResultContent()}
      title="Command Result"
      trigger="click"
      open={popoverOpen}
      onOpenChange={handlePopoverOpenChange}
      placement="right"
    >
      <Card
        hoverable
        className="computer-card"
        style={{
          border: isOnline ? "1px solid #52c41a" : "1px solid #f5222d",
        }}
        onDoubleClick={handleDoubleClick}
      >
        <div style={{ padding: "12px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
            <div style={{ display: "flex", alignItems: "center" }}>
              <DesktopOutlined style={{ fontSize: "20px", marginRight: "8px" }} />
              <Text strong style={{ fontSize: "16px" }}>{computer.name}</Text>
            </div>
            <Badge status={isOnline ? "success" : "error"} />
          </div>

          <Row gutter={[8, 8]} style={{ marginBottom: "8px" }}>
            <Col span={8}>
              <Tooltip title={`CPU Usage: ${cpuUsage}%`}>
                <Progress
                  percent={cpuUsage}
                  size="small"
                  strokeColor={getStatusColor(cpuUsage)}
                  showInfo={false}
                />
              </Tooltip>
            </Col>
            <Col span={8}>
              <Tooltip title={`RAM Usage: ${ramUsage}%`}>
                <Progress
                  percent={ramUsage}
                  size="small"
                  strokeColor={getStatusColor(ramUsage)}
                  showInfo={false}
                />
              </Tooltip>
            </Col>
            <Col span={8}>
              <Tooltip title={`Disk Usage: ${diskUsage}%`}>
                <Progress
                  percent={diskUsage}
                  size="small"
                  strokeColor={getStatusColor(diskUsage)}
                  showInfo={false}
                />
              </Tooltip>
            </Col>
          </Row>

          <div style={{ fontSize: "12px", color: "#666" }}>
            <div style={{ marginBottom: "4px" }}>
              <GlobalOutlined style={{ marginRight: "4px" }} />
              {computer.ip_address || "No IP"}
            </div>
            <div style={{ marginBottom: "4px" }}>
              <ClockCircleOutlined style={{ marginRight: "4px" }} />
              Last seen: {getTimeSinceLastSeen()}
            </div>
            <div style={{ marginBottom: "4px" }}>
              <DatabaseOutlined style={{ marginRight: "4px" }} />
              RAM: {formatRAMSize(computer.total_ram)}
            </div>
            <div>
              <HddOutlined style={{ marginRight: "4px" }} />
              Disk: {formatDiskSize(computer.total_disk_space)}
            </div>
          </div>

          {computer.have_active_errors && (
            <Tooltip title="Computer has errors requiring attention">
              <ExclamationCircleOutlined
                style={{
                  position: "absolute",
                  top: "8px",
                  right: "8px",
                  color: "#ff4d4f",
                  fontSize: "16px",
                }}
              />
            </Tooltip>
          )}

          {resultCount > 0 && (
            <Tooltip title="View command results">
              <Badge
                count={resultCount}
                style={{
                  position: "absolute",
                  bottom: "8px",
                  right: "8px",
                  cursor: "pointer",
                }}
                onClick={(e) => {
                  e.stopPropagation();
                  setPopoverOpen(true);
                }}
              />
            </Tooltip>
          )}
        </div>
      </Card>
    </Popover>
  );
};

export default ComputerCard;
