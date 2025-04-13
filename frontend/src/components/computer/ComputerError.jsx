import React, { useState, useEffect } from "react";
import { 
  Table, 
  Tag, 
  Button, 
  Typography, 
  Spin, 
  Empty, 
  Modal, 
  Form, 
  Input,
  message
} from "antd";
import { 
  ExclamationCircleOutlined, 
  CheckCircleOutlined, 
  ClockCircleOutlined,
  ReloadOutlined 
} from "@ant-design/icons";
import computerService from "../../services/computer.service";

const { Title, Text } = Typography;
const { TextArea } = Input;

const ComputerError = ({ computerId, onRefresh }) => {
  const [errors, setErrors] = useState([]);
  const [loading, setLoading] = useState(false);
  const [resolveModalVisible, setResolveModalVisible] = useState(false);
  const [currentError, setCurrentError] = useState(null);
  const [form] = Form.useForm();
  
  useEffect(() => {
    fetchErrors();
  }, [computerId]);

  const fetchErrors = async () => {
    if (!computerId) return;
    setLoading(true);
    try {
      const errorData = await computerService.getComputerErrors(computerId);
      setErrors(errorData);
    } catch (error) {
      console.error("Failed to fetch errors:", error);
      message.error("Failed to load computer errors. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleResolve = (error) => {
    setCurrentError(error);
    setResolveModalVisible(true);
  };

  const handleResolveSubmit = async () => {
    try {
      const values = await form.validateFields();
      await computerService.resolveComputerError(computerId, currentError.id, {
        resolution_notes: values.resolution_notes,
      });
      message.success("Error has been resolved");
      setResolveModalVisible(false);
      fetchErrors();
      if (onRefresh) onRefresh();
    } catch (error) {
      console.error("Failed to resolve error:", error);
      message.error("Failed to resolve error. Please try again.");
    }
  };

  const handleRefresh = () => {
    fetchErrors();
  };

  const getErrorTypeTag = (errorType) => {
    switch (errorType.toLowerCase()) {
      case "hardware":
        return <Tag color="volcano">Hardware</Tag>;
      case "software":
        return <Tag color="blue">Software</Tag>;
      case "network":
        return <Tag color="purple">Network</Tag>;
      case "system":
        return <Tag color="orange">System</Tag>;
      default:
        return <Tag>{errorType}</Tag>;
    }
  };

  const columns = [
    {
      title: "Type",
      dataIndex: "error_type",
      key: "error_type",
      render: (text) => getErrorTypeTag(text),
    },
    {
      title: "Message",
      dataIndex: "error_message",
      key: "error_message",
    },
    {
      title: "Reported At",
      dataIndex: "reported_at",
      key: "reported_at",
      render: (text) => new Date(text).toLocaleString(),
    },
    {
      title: "Status",
      key: "status",
      render: (_, record) => (
        record.resolved ? 
          <Tag color="success" icon={<CheckCircleOutlined />}>Resolved</Tag> : 
          <Tag color="error" icon={<ClockCircleOutlined />}>Pending</Tag>
      ),
    },
    {
      title: "Action",
      key: "action",
      render: (_, record) => (
        !record.resolved && (
          <Button type="primary" size="small" onClick={() => handleResolve(record)}>
            Resolve
          </Button>
        )
      ),
    },
  ];

  return (
    <div className="computer-errors">
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16 }}>
        <Title level={4}>
          <ExclamationCircleOutlined /> Error History
        </Title>
        <Button icon={<ReloadOutlined />} onClick={handleRefresh}>
          Refresh
        </Button>
      </div>
      
      {loading ? (
        <div style={{ textAlign: "center", padding: "20px" }}>
          <Spin size="large" />
        </div>
      ) : errors.length > 0 ? (
        <Table 
          columns={columns} 
          dataSource={errors.map(error => ({ ...error, key: error.id }))}
          pagination={{ pageSize: 5 }}
        />
      ) : (
        <Empty description="No errors reported for this computer" />
      )}

      <Modal
        title="Resolve Error"
        open={resolveModalVisible}
        onOk={handleResolveSubmit}
        onCancel={() => {
          setResolveModalVisible(false);
          form.resetFields();
        }}
        okText="Resolve"
        destroyOnClose
      >
        {currentError && (
          <>
            <div style={{ marginBottom: 16 }}>
              <Text strong>Error Type:</Text> {getErrorTypeTag(currentError.error_type)}
              <br />
              <Text strong>Message:</Text> {currentError.error_message}
              <br />
              <Text strong>Reported At:</Text> {new Date(currentError.reported_at).toLocaleString()}
            </div>
            <Form form={form} layout="vertical" preserve={false}>
              <Form.Item
                name="resolution_notes"
                label="Resolution Notes"
                rules={[
                  { required: true, message: "Please enter resolution notes" },
                ]}
              >
                <TextArea rows={4} placeholder="Enter details about how this error was resolved" />
              </Form.Item>
            </Form>
          </>
        )}
      </Modal>
    </div>
  );
};

export default ComputerError;