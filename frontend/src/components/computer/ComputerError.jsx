import React, { useState, useCallback } from "react";
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
  Select,
  message,
  Space
} from "antd";
import {
  ExclamationCircleOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  PlusOutlined,
  EyeOutlined,
  MinusCircleOutlined
} from "@ant-design/icons";
import computerService from "../../services/computer.service";
import { useSimpleFetch } from "../../hooks/useSimpleFetch";
import { useModalState } from "../../hooks/useModalState";
import { useFormatting } from "../../hooks/useFormatting";

const { Title, Text, Paragraph } = Typography;
const { TextArea } = Input;
const { Option } = Select;

const STANDARDIZED_ERROR_TYPES = [
  "Hardware", "Software", "Network", "System", "Security", "Other"
];

/**
 * Renders a list of error details.
 * @param {object|null} details - The error details object.
 * @returns {React.ReactElement} A paragraph or an unordered list of details.
 */
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

/**
 * Component to display and manage errors for a specific computer.
 * Allows viewing error history, reporting new errors, and resolving existing ones.
 *
 * @component
 * @param {object} props - Component props.
 * @param {string|number} props.computerId - The ID of the computer whose errors are being managed.
 * @param {Function} [props.onRefresh] - Optional callback function triggered after resolving an error to refresh parent data.
 * @returns {React.ReactElement} The rendered ComputerError component.
 */
const ComputerError = ({ computerId, onRefresh }) => {
  const { formatTimestamp } = useFormatting();

  const fetchErrorsCallback = useCallback(() => {
    if (!computerId) return Promise.resolve([]);
    return computerService.getComputerErrors(computerId);
  }, [computerId]);

  const { data: errors, loading, error: fetchError, refresh: fetchErrors, setData: setErrors } = useSimpleFetch(
    fetchErrorsCallback,
    [fetchErrorsCallback],
    { errorMessage: 'Failed to load computer errors' }
  );

  const { isModalVisible: resolveModalVisible, selectedItem: currentError, openModal: openResolveModal, closeModal: closeResolveModal, setSelectedItem: setCurrentError } = useModalState();
  const { isModalVisible: reportModalVisible, openModal: openReportModal, closeModal: closeReportModal } = useModalState();
  const { isModalVisible: errorDetailsModalVisible, openModal: openErrorDetailsModal, closeModal: closeErrorDetailsModal } = useModalState();
  const { isModalVisible: resolutionNotesModalVisible, openModal: openResolutionNotesModal, closeModal: closeResolutionNotesModal } = useModalState();

  const [resolveForm] = Form.useForm();
  const [reportForm] = Form.useForm();

  const [actionLoading, setActionLoading] = useState(false);
  const [viewingError, setViewingError] = useState(null);

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

  const handleCloseErrorDetailsModal = () => {
    closeErrorDetailsModal();
    setViewingError(null);
  };

  const handleCloseResolutionNotesModal = () => {
    closeResolutionNotesModal();
    setViewingError(null);
  };

  const handleResolveSubmit = async () => {
    try {
      const values = await resolveForm.validateFields();
      setActionLoading(true);
      await computerService.resolveComputerError(computerId, currentError.id, {
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

      await computerService.reportComputerError(computerId, {
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

  const handleRefresh = () => {
    fetchErrors();
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

  const columns = [
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

  return (
    <div className="computer-errors" style={{ padding: '20px', background: '#f0f2f5', borderRadius: '8px' }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: 'center', marginBottom: 16 }}>
        <Title level={4} style={{ margin: 0 }}>
          <ExclamationCircleOutlined style={{ marginRight: 8 }}/> Error History
        </Title>
        <div>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenReportModal} style={{ marginRight: 8 }} disabled={!computerId || loading || actionLoading}>
            Report New Error
          </Button>
          <Button icon={<ReloadOutlined />} onClick={handleRefresh} disabled={loading || actionLoading}>
            Refresh
          </Button>
        </div>
      </div>

      {loading ? (
        <div style={{ textAlign: "center", padding: "50px" }}>
          <Spin size="large" />
        </div>
      ) : fetchError ? (
         <Empty description={fetchError || "Failed to load errors"} />
      ) : (errors || []).length > 0 ? (
        <Table
          columns={columns}
          dataSource={(errors || []).map(error => ({ ...error, key: error.id }))}
          pagination={{ pageSize: 5, showSizeChanger: true, pageSizeOptions: ['5', '10', '20'], size: 'small' }}
          loading={loading}
          size="small"
          rowClassName={(record) => !record.resolved ? 'table-row-pending' : ''}
          scroll={{ x: 700 }}
        />
      ) : (
        <Empty description={computerId ? "No errors reported for this computer" : "Select a computer to view errors"} />
      )}

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
                         <Input placeholder="Key (e.g., IP Address)" style={{ width: '180px' }}/>
                       </Form.Item>
                       <Form.Item
                         {...restField}
                         name={[name, 'value']}
                          style={{ marginBottom: 0 }}
                       >
                         <Input placeholder="Value (e.g., 192.168.1.10)" style={{ width: '250px' }}/>
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
        onCancel={handleCloseErrorDetailsModal}
        footer={[ <Button key="back" onClick={handleCloseErrorDetailsModal}>Close</Button> ]}
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
        onCancel={handleCloseResolutionNotesModal}
        footer={[ <Button key="back" onClick={handleCloseResolutionNotesModal}>Close</Button> ]}
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

export default ComputerError;
