import React, { useState, useCallback } from "react";
import {
  Card,
  Button,
  Space,
  Popconfirm,
  message,
  Tooltip,
  Badge,
  Tag,
  Typography,
  Row,
  Col,
  Divider,
  Progress,
  Table,
  Modal,
  Form,
  Input,
  Select,
  Spin,
  Empty
} from "antd";
import {
  DeleteOutlined,
  GlobalOutlined,
  HomeOutlined,
  InfoCircleOutlined,
  ClockCircleOutlined,
  LaptopOutlined,
  DatabaseOutlined,
  HddOutlined,
  RocketOutlined,
  ExclamationCircleOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  PlusOutlined,
  EyeOutlined,
  MinusCircleOutlined
} from "@ant-design/icons";
import { useAuth } from "../../contexts/AuthContext";
import computerService from "../../services/computer.service";
import { useNavigate } from "react-router-dom";
import { useFormatting } from "../../hooks/useFormatting";
import { useSimpleFetch } from "../../hooks/useSimpleFetch";
import { useModalState } from "../../hooks/useModalState";

const { Text, Title, Paragraph } = Typography;
const { TextArea } = Input;
const { Option } = Select;

const STANDARDIZED_ERROR_TYPES = [
  "Hardware", "Software", "Network", "System", "Security", "Other"
];

const cardStyle = {
  height: '180px',
  width: '100%',
  overflow: 'hidden',
  borderRadius: '8px',
  boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
  display: 'flex',
  flexDirection: 'column'
};

const renderErrorDetails = (details) => {
  if (!details || Object.keys(details).length === 0) {
    return <Paragraph>No additional details provided.</Paragraph>;
  }
  return (
    <ul style={{ paddingLeft: '20px', listStyle: 'disc' }}>
      {Object.entries(details).map(([key, value]) => (
        <li key={key}>
          <Text strong>{key}:</Text>{' '}
          {typeof value === 'object' && value !== null
            ? JSON.stringify(value)
            : String(value || '')}
        </li>
      ))}
    </ul>
  );
};

const ComputerDetail = ({ computer, isOnline, cpuUsage, ramUsage, diskUsage, onRefresh }) => {
  const { isAdmin } = useAuth();
  const navigate = useNavigate();
  const { formatRAMSize, formatDiskSize, formatTimestamp, getTimeAgo, getStatusColor } = useFormatting();

  // Error management state
  const [actionLoading, setActionLoading] = useState(false);
  const [viewingError, setViewingError] = useState(null);

  const { isModalVisible: resolveModalVisible, selectedItem: currentError, openModal: openResolveModal, closeModal: closeResolveModal, setSelectedItem: setCurrentError } = useModalState();
  const { isModalVisible: reportModalVisible, openModal: openReportModal, closeModal: closeReportModal } = useModalState();
  const { isModalVisible: errorDetailsModalVisible, openModal: openErrorDetailsModal, closeModal: closeErrorDetailsModal } = useModalState();
  const { isModalVisible: resolutionNotesModalVisible, openModal: openResolutionNotesModal, closeModal: closeResolutionNotesModal } = useModalState();

  const [resolveForm] = Form.useForm();
  const [reportForm] = Form.useForm();

  const fetchErrorsCallback = useCallback(() => {
    if (!computer?.id) return Promise.resolve([]);
    return computerService.getComputerErrors(computer.id);
  }, [computer?.id]);

  const { data: errors, loading: errorsLoading, error: fetchError, refresh: fetchErrors } = useSimpleFetch(
    fetchErrorsCallback,
    [fetchErrorsCallback],
    { errorMessage: 'Failed to load computer errors' }
  );

  const handleDelete = async () => {
    if (!computer?.id) return;
    try {
      await computerService.deleteComputer(computer.id);
      message.success('Computer deleted successfully');
      if (computer.room?.id) {
        navigate(`/rooms/${computer.room.id}`);
      } else {
        navigate('/computers');
      }
      if (onRefresh) onRefresh();
    } catch (error) {
      message.error('Failed to delete computer');
      console.error('Error deleting computer:', error);
    }
  };

  const getTimeSinceLastSeen = () => {
    if (!computer.last_update) return 'Never';
    return getTimeAgo(computer.last_update);
  };

  const handleOpenResolveModal = (error) => {
    resolveForm.setFieldsValue({ resolution_notes: '' });
    openResolveModal('resolve', error);
  };

  const handleOpenReportModal = () => {
    reportForm.resetFields();
    openReportModal('report');
  };

  const handleOpenErrorDetailsModal = (error) => {
    setViewingError(error);
    openErrorDetailsModal('details');
  };

  const handleOpenResolutionNotesModal = (error) => {
    setViewingError(error);
    openResolutionNotesModal('notes');
  };

  const handleResolveSubmit = async () => {
    try {
      const values = await resolveForm.validateFields();
      setActionLoading(true);
      await computerService.resolveComputerError(computer.id, currentError.id, {
        resolution_notes: values.resolution_notes,
      });
      message.success("Error has been resolved successfully!");
      closeResolveModal();
      fetchErrors();
      if (onRefresh) onRefresh();
    } catch (error) {
      console.error("Failed to resolve error:", error);
      message.error(error.message || "Failed to resolve error. Please try again.");
    } finally {
      setActionLoading(false);
    }
  };

  const handleReportSubmit = async () => {
    try {
      const values = await reportForm.validateFields();
      setActionLoading(true);

      const errorDetailsObject = {};
      if (values.error_details_list) {
        values.error_details_list.forEach(item => {
          if (item && item.key) {
            errorDetailsObject[item.key] = item.value;
          }
        });
      }

      await computerService.reportComputerError(computer.id, {
        error_type: values.error_type,
        error_message: values.error_message,
        error_details: errorDetailsObject,
      });

      message.success("New error reported successfully!");
      closeReportModal();
      fetchErrors();
    } catch (error) {
      console.error("Failed to report error:", error);
      if (error.errorFields) {
        message.error("Please fill in all required fields correctly.");
      } else {
        message.error(error.message || "Failed to report error. Please try again.");
      }
    } finally {
      setActionLoading(false);
    }
  };

  const getErrorTypeTag = (errorType) => {
    if (!errorType) return <Tag>Unknown</Tag>;
    const lowerCaseType = errorType.toLowerCase();
    switch (lowerCaseType) {
      case "hardware": return <Tag color="volcano">{errorType}</Tag>;
      case "software": return <Tag color="blue">{errorType}</Tag>;
      case "network": return <Tag color="purple">{errorType}</Tag>;
      case "system": return <Tag color="orange">{errorType}</Tag>;
      case "security": return <Tag color="red">{errorType}</Tag>;
      case "other": return <Tag color="default">{errorType}</Tag>;
      default: return <Tag>{errorType}</Tag>;
    }
  };

  const errorColumns = [
    {
      title: "Type",
      dataIndex: "error_type",
      key: "error_type",
      render: (text) => getErrorTypeTag(text),
      filters: STANDARDIZED_ERROR_TYPES.map(type => ({ text: type, value: type })),
      onFilter: (value, record) => record.error_type === value,
      width: 120,
    },
    {
      title: "Message / Details",
      dataIndex: "error_message",
      key: "message_details",
      render: (_, record) => (
        <Button
          type="link"
          icon={<EyeOutlined />}
          onClick={() => handleOpenErrorDetailsModal(record)}
          style={{ padding: 0 }}
        >
          View Details
        </Button>
      ),
    },
    {
      title: "Reported At",
      dataIndex: "reported_at",
      key: "reported_at",
      render: (text) => formatTimestamp(text),
      sorter: (a, b) => new Date(a.reported_at) - new Date(b.reported_at),
      defaultSortOrder: 'descend',
      width: 180,
    },
    {
      title: "Status",
      key: "status",
      dataIndex: 'resolved',
      render: (resolved) => (
        resolved ? (
          <Tag color="success" icon={<CheckCircleOutlined />}>Resolved</Tag>
        ) : (
          <Tag color="error" icon={<ClockCircleOutlined />}>Pending</Tag>
        )
      ),
      filters: [
        { text: 'Pending', value: false },
        { text: 'Resolved', value: true },
      ],
      onFilter: (value, record) => record.resolved === value,
      width: 120,
    },
    {
      title: "Action",
      key: "action",
      render: (_, record) => (
        record.resolved ? (
          <Button
            type="default"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleOpenResolutionNotesModal(record)}
            disabled={!record.resolution_notes}
          >
            View Resolution
          </Button>
        ) : (
          <Button
            type="primary"
            size="small"
            onClick={() => handleOpenResolveModal(record)}
            loading={actionLoading && resolveModalVisible && currentError?.id === record.id}
          >
            Resolve
          </Button>
        )
      ),
      width: 150,
    },
  ];

  if (!computer) return null;

  return (
    <div className="computer-detail">
      <Card
        hoverable
        className="computer-card-detailed"
        style={{
          ...cardStyle,
          height: 'auto',
          border: isOnline ? '1px solid #52c41a' : '1px solid #f5222d',
          marginBottom: '20px'
        }}
        cover={
          <div style={{
            background: 'linear-gradient(135deg, #1890ff 0%, #096dd9 100%)',
            padding: '20px',
            position: 'relative',
            height: '100px',
            display: 'flex',
            alignItems: 'center'
          }}>
            <LaptopOutlined style={{ fontSize: '48px', color: 'white', marginRight: '15px' }} />
            <div>
              <Title level={4} style={{ color: 'white', margin: '0' }}>{computer.name}</Title>
              <Space>
                <Badge status={isOnline ? "success" : "error"} />
                <Text style={{ color: 'white' }}>
                  {isOnline ? "Online" : "Offline"}
                </Text>
                {computer.have_active_errors && (
                  <Tooltip title="Computer has errors requiring attention">
                    <ExclamationCircleOutlined
                      style={{
                        color: '#ff4d4f',
                        fontSize: '16px',
                        backgroundColor: 'white',
                        borderRadius: '50%',
                        padding: '2px',
                        cursor: 'pointer'
                      }}
                      onClick={(e) => {
                        e.stopPropagation();
                        navigate(`/computers/${computer.id}?tab=errors`);
                      }}
                    />
                  </Tooltip>
                )}
                {computer.room?.name && (
                  <Tag color="blue">
                    <HomeOutlined /> {computer.room.name}
                  </Tag>
                )}
              </Space>
            </div>
            <div style={{ position: 'absolute', right: '15px', top: '15px' }}>
              <Space>
                {isAdmin && (
                  <Tooltip title="Delete computer">
                    <Popconfirm
                      title="Are you sure you want to delete this computer?"
                      onConfirm={handleDelete}
                      okText="Yes"
                      cancelText="No"
                    >
                      <Button
                        type="text"
                        danger
                        shape="circle"
                        icon={<DeleteOutlined style={{ color: 'white', fontSize: '18px' }} />}
                      />
                    </Popconfirm>
                  </Tooltip>
                )}
              </Space>
            </div>
          </div>
        }
        styles={{
          body: { padding: 0 }
        }}
      >
        <div style={{ padding: '20px' }}>
          <Row gutter={16} style={{ marginBottom: '20px', textAlign: 'center' }}>
            <Col span={8}>
              <Tooltip title={`CPU Usage: ${cpuUsage}%`}>
                <Progress
                  type="dashboard"
                  percent={cpuUsage}
                  size={80}
                  format={() => 'CPU'}
                  strokeColor={getStatusColor(cpuUsage)}
                />
              </Tooltip>
            </Col>
            <Col span={8}>
              <Tooltip title={`RAM Usage: ${ramUsage}%`}>
                <Progress
                  type="dashboard"
                  percent={ramUsage}
                  size={80}
                  format={() => 'RAM'}
                  strokeColor={getStatusColor(ramUsage)}
                />
              </Tooltip>
            </Col>
            <Col span={8}>
              <Tooltip title={`Disk Usage: ${diskUsage}%`}>
                <Progress
                  type="dashboard"
                  percent={diskUsage}
                  size={80}
                  format={() => 'Disk'}
                  strokeColor={getStatusColor(diskUsage)}
                />
              </Tooltip>
            </Col>
          </Row>

          <Divider style={{ margin: '10px 0 20px 0' }} />

          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12}>
              <Space align="center">
                <GlobalOutlined />
                <Text>{computer.ip_address || 'No IP'}</Text>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <ClockCircleOutlined />
                <Tooltip title={`Specific time: ${formatTimestamp(computer.last_update)}`}>
                  <Text>Last seen: {getTimeSinceLastSeen()}</Text>
                </Tooltip>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <InfoCircleOutlined />
                <Tooltip title={computer.cpu_info || 'CPU Unknown'}>
                  <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.cpu_info || 'CPU Unknown'}
                  </Text>
                </Tooltip>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <LaptopOutlined />
                <Tooltip title={computer.os_info || 'OS Unknown'}>
                  <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.os_info || 'OS Unknown'}
                  </Text>
                </Tooltip>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <DatabaseOutlined />
                <Text>RAM: {formatRAMSize(computer.total_ram)}</Text>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <HddOutlined />
                <Text>Disk: {formatDiskSize(computer.total_disk_space)}</Text>
              </Space>
            </Col>
            <Col xs={24} sm={12}>
              <Space align="center">
                <RocketOutlined />
                <Tooltip title={computer.gpu_info || 'GPU Unknown'}>
                  <Text style={{ maxWidth: '180px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {computer.gpu_info || 'GPU Unknown'}
                  </Text>
                </Tooltip>
              </Space>
            </Col>
            {computer.room?.name && (
              <Col xs={24} sm={12}>
                <Space align="center">
                  <HomeOutlined />
                  <Text>Room: {computer.room.name}</Text>
                </Space>
              </Col>
            )}
          </Row>

          <Divider style={{ margin: '20px 0 10px 0' }} />
          <Row align="middle" gutter={[8, 8]}>
            <Col span={24}>
              <Space>
                <InfoCircleOutlined />
                <Text type="secondary">Agent ID: {computer.unique_agent_id || 'Not registered'}</Text>
              </Space>
            </Col>
          </Row>
        </div>
      </Card>

      <div className="computer-errors" style={{ padding: '20px', background: '#f0f2f5', borderRadius: '8px' }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: 'center', marginBottom: 16 }}>
          <Title level={4} style={{ margin: 0 }}>
            <ExclamationCircleOutlined style={{ marginRight: 8 }} /> Error History
          </Title>
          <div>
            <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenReportModal} style={{ marginRight: 8 }} disabled={!computer.id || errorsLoading || actionLoading}>
              Report New Error
            </Button>
            <Button icon={<ReloadOutlined />} onClick={fetchErrors} disabled={errorsLoading || actionLoading}>
              Refresh
            </Button>
          </div>
        </div>

        {errorsLoading ? (
          <div style={{ textAlign: "center", padding: "50px" }}>
            <Spin size="large" />
          </div>
        ) : fetchError ? (
          <Empty description={fetchError || "Failed to load errors"} />
        ) : (errors || []).length > 0 ? (
          <Table
            columns={errorColumns}
            dataSource={(errors || []).map(error => ({ ...error, key: error.id }))}
            pagination={{ pageSize: 5, showSizeChanger: true, pageSizeOptions: ['5', '10', '20'], size: 'small' }}
            loading={errorsLoading}
            size="small"
            rowClassName={(record) => !record.resolved ? 'table-row-pending' : ''}
            scroll={{ x: 700 }}
          />
        ) : (
          <Empty description="No errors reported for this computer" />
        )}
      </div>

      {/* Error Modals */}
      <Modal
        title="Resolve Error"
        open={resolveModalVisible}
        onOk={handleResolveSubmit}
        onCancel={closeResolveModal}
        confirmLoading={actionLoading}
        okText="Mark as Resolved"
        destroyOnClose
      >
        {currentError && (
          <>
            <div style={{ marginBottom: 16 }}>
              <Text strong>Error Type:</Text> {getErrorTypeTag(currentError.error_type)} <br />
              <Text strong>Message:</Text> {currentError.error_message} <br />
              <Text strong>Reported At:</Text> {formatTimestamp(currentError.reported_at)}
            </div>
            <Form form={resolveForm} layout="vertical" preserve={false}>
              <Form.Item name="resolution_notes" label="Resolution Notes" rules={[{ required: true, message: "Please enter resolution notes" }]}>
                <TextArea rows={4} placeholder="Enter details about how this error was resolved" />
              </Form.Item>
            </Form>
          </>
        )}
      </Modal>

      <Modal
        title="Report New Error"
        open={reportModalVisible}
        onOk={handleReportSubmit}
        onCancel={closeReportModal}
        confirmLoading={actionLoading}
        okText="Report Error"
        destroyOnClose
        width={600}
      >
        <Form form={reportForm} layout="vertical" preserve={false} initialValues={{ error_details_list: [{ key: '', value: '' }] }}>
          <Form.Item name="error_type" label="Error Type" rules={[{ required: true, message: "Please select the error type" }]}>
            <Select placeholder="Select error type">
              {STANDARDIZED_ERROR_TYPES.map(type => (<Option key={type} value={type}>{type}</Option>))}
            </Select>
          </Form.Item>
          <Form.Item name="error_message" label="Error Message" rules={[{ required: true, message: "Please describe the error" }]}>
            <TextArea rows={3} placeholder="Describe the error encountered" />
          </Form.Item>

          <Form.Item label="Error Details (Optional Key-Value Pairs)">
            <Form.List name="error_details_list">
              {(fields, { add, remove }) => (
                <>
                  {fields.map(({ key, name, ...restField }) => (
                    <Space key={key} style={{ display: 'flex', marginBottom: 8 }} align="baseline">
                      <Form.Item
                        {...restField}
                        name={[name, 'key']}
                        style={{ marginBottom: 0 }}
                      >
                        <Input placeholder="Key (e.g., IP Address)" style={{ width: '180px' }} />
                      </Form.Item>
                      <Form.Item
                        {...restField}
                        name={[name, 'value']}
                        style={{ marginBottom: 0 }}
                      >
                        <Input placeholder="Value (e.g., 192.168.1.10)" style={{ width: '250px' }} />
                      </Form.Item>
                      <MinusCircleOutlined onClick={() => remove(name)} />
                    </Space>
                  ))}
                  <Form.Item>
                    <Button type="dashed" onClick={() => add()} block icon={<PlusOutlined />}>
                      Add Detail Field
                    </Button>
                  </Form.Item>
                </>
              )}
            </Form.List>
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="Error Details"
        open={errorDetailsModalVisible}
        onCancel={() => {
          closeErrorDetailsModal();
          setViewingError(null);
        }}
        footer={[<Button key="back" onClick={() => {
          closeErrorDetailsModal();
          setViewingError(null);
        }}>Close</Button>]}
      >
        {viewingError && (
          <>
            <Paragraph>
              <Text strong>Type:</Text> {getErrorTypeTag(viewingError.error_type)}
            </Paragraph>
            <Paragraph>
              <Text strong>Reported At:</Text> {formatTimestamp(viewingError.reported_at)}
            </Paragraph>
            <Paragraph>
              <Text strong>Message:</Text>
              <Paragraph style={{ marginTop: '4px', paddingLeft: '10px', borderLeft: '3px solid #eee' }}>
                {viewingError.error_message || "N/A"}
              </Paragraph>
            </Paragraph>
            <Paragraph>
              <Text strong>Additional Details:</Text>
              {renderErrorDetails(viewingError.error_details)}
            </Paragraph>
          </>
        )}
      </Modal>

      <Modal
        title="Resolution Notes"
        open={resolutionNotesModalVisible}
        onCancel={() => {
          closeResolutionNotesModal();
          setViewingError(null);
        }}
        footer={[<Button key="back" onClick={() => {
          closeResolutionNotesModal();
          setViewingError(null);
        }}>Close</Button>]}
      >
        {viewingError && (
          <>
            <Paragraph>
              <Text strong>Type:</Text> {getErrorTypeTag(viewingError.error_type)}
            </Paragraph>
            <Paragraph>
              <Text strong>Resolved At:</Text> {viewingError.resolved_at ? formatTimestamp(viewingError.resolved_at) : "N/A"}
            </Paragraph>
            <Paragraph>
              <Text strong>Notes:</Text>
              <Paragraph style={{ marginTop: '4px', paddingLeft: '10px', borderLeft: '3px solid #eee' }}>
                {viewingError.resolution_notes || "No resolution notes provided."}
              </Paragraph>
            </Paragraph>
          </>
        )}
      </Modal>

      <style>{`
        .table-row-pending td {
          background-color: #fff2f0;
        }
        .ant-table-thead > tr > th {
          background-color: #fafafa !important;
          font-weight: bold;
        }
        .ant-form-item {
          margin-bottom: 16px;
        }
      `}</style>
    </div>
  );
};

export default ComputerDetail; 