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
  Select,
  message,
  Space // Import Space for Form.List layout
} from "antd";
import {
  ExclamationCircleOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  PlusOutlined,
  EyeOutlined, // Icon for View buttons
  MinusCircleOutlined // Icon for removing Form.List item
} from "@ant-design/icons";
// Giả sử computerService được import đúng cách
import computerService from "../../services/computer.service";

// // *** Mock computerService for demonstration (Updated) ***
// const computerService = {
//   getComputerErrors: async (computerId) => {
//     console.log(`Fetching errors for computer ${computerId}`);
//     await new Promise(resolve => setTimeout(resolve, 500));
//     // Sample data including error_details and resolution_notes
//     const mockErrors = {
//         1: [
//             { id: 101, error_type: 'Hardware', error_message: 'CPU Overheating causing shutdown.', reported_at: new Date(Date.now() - 7200000).toISOString(), resolved: false, error_details: { temperature: '95C', core: 'CPU0', threshold: '90C' } },
//             { id: 102, error_type: 'Software', error_message: 'Application crashed unexpectedly.', reported_at: new Date(Date.now() - 86400000).toISOString(), resolved: true, resolved_at: new Date(Date.now() - 43200000).toISOString(), resolution_notes: 'Restarted the application service and applied patch v1.2.', error_details: { process_id: '12345', memory_usage: '2.5GB', exception_code: '0xC0000005' } },
//             { id: 103, error_type: 'Network', error_message: 'Cannot connect to internal server.', reported_at: new Date(Date.now() - 3600000).toISOString(), resolved: false, error_details: { target_server: 'SRV-DATA01', ip_address: '192.168.1.100', port: '1433', ping_status: 'timeout' } },
//             { id: 104, error_type: 'Security', error_message: 'Unauthorized login attempt detected.', reported_at: new Date(Date.now() - 1800000).toISOString(), resolved: true, resolved_at: new Date().toISOString(), resolution_notes: 'Blocked source IP address at firewall. User confirmed it was not them.', error_details: { source_ip: '10.20.30.40', username: 'admin', attempt_time: new Date(Date.now() - 1800000).toISOString() } },
//         ],
//         2: [], // No errors for computer 2 initially
//         // Add more computer IDs if needed
//     };
//     return mockErrors[computerId] || [];
//   },
//   resolveComputerError: async (computerId, errorId, data) => {
//     console.log(`Resolving error ${errorId} for computer ${computerId} with notes: ${data.resolution_notes}`);
//     await new Promise(resolve => setTimeout(resolve, 500));
//     // Find the error in mock data to return updated info (optional, good for consistency)
//     // For simplicity, just return a generic success structure
//     return {
//         error: {
//             id: errorId,
//             error_type: 'Unknown', // In a real scenario, fetch the actual error type
//             error_message: 'Error resolved',
//             reported_at: new Date().toISOString(),
//             resolved: true,
//             resolved_at: new Date().toISOString(),
//             resolution_notes: data.resolution_notes,
//             error_details: {} // Details might not change upon resolution
//         },
//         computerId: computerId
//     };
//   },
//   reportComputerError: async (computerId, errorData) => {
//      console.log(`Reporting new error for computer ${computerId}:`, errorData); // Log includes error_details now
//      await new Promise(resolve => setTimeout(resolve, 500));
//      const newError = {
//          id: Math.floor(Math.random() * 1000) + 200,
//          error_type: errorData.error_type,
//          error_message: errorData.error_message,
//          error_details: errorData.error_details || {}, // Include details
//          reported_at: new Date().toISOString(),
//          resolved: false,
//          resolved_at: null,
//          resolution_notes: null
//      };
//      // In a real app, you might want to add this to your mock data source if testing locally
//      return { error: newError, computerId: computerId };
//   }
// };
// // *** End Mock computerService ***

const { Title, Text, Paragraph } = Typography; // Add Paragraph
const { TextArea } = Input;
const { Option } = Select;

const STANDARDIZED_ERROR_TYPES = [
  "Hardware", "Software", "Network", "System", "Security", "Other"
];

// Helper function to render error details object
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

const ComputerError = ({ computerId, onRefresh }) => {
  const [errors, setErrors] = useState([]);
  const [loading, setLoading] = useState(false);
  // Modal visibility states
  const [resolveModalVisible, setResolveModalVisible] = useState(false);
  const [reportModalVisible, setReportModalVisible] = useState(false);
  const [errorDetailsModalVisible, setErrorDetailsModalVisible] = useState(false); // State for error details modal
  const [resolutionNotesModalVisible, setResolutionNotesModalVisible] = useState(false); // State for resolution notes modal

  const [currentError, setCurrentError] = useState(null); // Holds the error being interacted with
  const [resolveForm] = Form.useForm();
  const [reportForm] = Form.useForm();

  useEffect(() => {
    fetchErrors();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [computerId]);

  const fetchErrors = async () => {
    if (!computerId) {
        setErrors([]);
        return;
    };
    setLoading(true);
    try {
      const errorData = await computerService.getComputerErrors(computerId);
      setErrors(errorData || []);
    } catch (error) {
      console.error("Failed to fetch errors:", error);
      message.error("Failed to load computer errors. Please try again.");
      setErrors([]);
    } finally {
      setLoading(false);
    }
  };

  // --- Modal Open Handlers ---
  const handleOpenResolveModal = (error) => {
    setCurrentError(error);
    resolveForm.setFieldsValue({ resolution_notes: '' });
    setResolveModalVisible(true);
  };

  const handleOpenReportModal = () => {
    reportForm.resetFields(); // Reset form including error details list
    setReportModalVisible(true);
  };

  const handleOpenErrorDetailsModal = (error) => {
    setCurrentError(error);
    setErrorDetailsModalVisible(true);
  };

  const handleOpenResolutionNotesModal = (error) => {
    setCurrentError(error);
    setResolutionNotesModalVisible(true);
  };

  // --- Form Submit Handlers ---
  const handleResolveSubmit = async () => {
    try {
      const values = await resolveForm.validateFields();
      setLoading(true);
      await computerService.resolveComputerError(computerId, currentError.id, {
        resolution_notes: values.resolution_notes,
      });
      message.success("Error has been resolved successfully!");
      setResolveModalVisible(false);
      fetchErrors();
      if (onRefresh) onRefresh();
    } catch (error) {
      console.error("Failed to resolve error:", error);
      message.error(error.message || "Failed to resolve error. Please try again.");
    } finally {
       setLoading(false);
    }
  };

  const handleReportSubmit = async () => {
    try {
      const values = await reportForm.validateFields();
      setLoading(true);

      // Convert error_details_list from Form.List [{key: k, value: v}] to object {k: v}
      const errorDetailsObject = {};
      if (values.error_details_list) {
        values.error_details_list.forEach(item => {
          if (item && item.key) { // Ensure item and key exist
            errorDetailsObject[item.key] = item.value;
          }
        });
      }

      await computerService.reportComputerError(computerId, {
        error_type: values.error_type,
        error_message: values.error_message,
        error_details: errorDetailsObject, // Send the converted object
      });

      message.success("New error reported successfully!");
      setReportModalVisible(false);
      fetchErrors();
      if (onRefresh) onRefresh();
    } catch (error) {
      console.error("Failed to report error:", error);
      // Handle validation errors specifically if needed
      if (error.errorFields) {
           message.error("Please fill in all required fields correctly.");
      } else {
           message.error(error.message || "Failed to report error. Please try again.");
      }
    } finally {
       setLoading(false);
    }
  };

  // --- Refresh Logic ---
  const handleRefresh = () => {
    fetchErrors();
  };

  // --- Helper Functions ---
  const getErrorTypeTag = (errorType) => {
    // (Function remains the same as before)
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

  // --- Table Columns (Updated) ---
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
      title: "Message / Details", // Updated title
      dataIndex: "error_message",
      key: "message_details",
      render: (_, record) => ( // Render a button instead of text
        <Button
          type="link"
          icon={<EyeOutlined />}
          onClick={() => handleOpenErrorDetailsModal(record)}
          style={{ padding: 0 }} // Remove padding for link-like appearance
        >
          View Details
        </Button>
      ),
    },
    {
      title: "Reported At",
      dataIndex: "reported_at",
      key: "reported_at",
      render: (text) => text ? new Date(text).toLocaleString() : '-',
      sorter: (a, b) => new Date(a.reported_at) - new Date(b.reported_at),
      defaultSortOrder: 'descend',
      width: 180,
    },
    {
      title: "Status",
      key: "status",
      dataIndex: 'resolved', // Use dataIndex for filtering/sorting
      render: (resolved) => ( // Render based on resolved status
        resolved ?
          <Tag color="success" icon={<CheckCircleOutlined />}>Resolved</Tag> :
          <Tag color="error" icon={<ClockCircleOutlined />}>Pending</Tag>
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
          // Show "View Resolution" button if resolved
          <Button
            type="default" // Or "link"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleOpenResolutionNotesModal(record)}
            disabled={!record.resolution_notes} // Disable if no notes
          >
            View Resolution
          </Button>
        ) : (
          // Show "Resolve" button if pending
          <Button
            type="primary"
            size="small"
            onClick={() => handleOpenResolveModal(record)}
          >
            Resolve
          </Button>
        )
      ),
      width: 150,
    },
  ];

  // --- Render Component ---
  return (
    <div className="computer-errors" style={{ padding: '20px', background: '#f0f2f5', borderRadius: '8px' }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: 'center', marginBottom: 16 }}>
        <Title level={4} style={{ margin: 0 }}>
          <ExclamationCircleOutlined style={{ marginRight: 8 }}/> Error History
        </Title>
        <div>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenReportModal} style={{ marginRight: 8 }} disabled={!computerId || loading}>
            Report New Error
          </Button>
          <Button icon={<ReloadOutlined />} onClick={handleRefresh} disabled={loading}>
            Refresh
          </Button>
        </div>
      </div>

      {/* Error Table */}
      {loading && !resolveModalVisible && !reportModalVisible && !errorDetailsModalVisible && !resolutionNotesModalVisible ? ( // Show main spinner only if no modals are open
        <div style={{ textAlign: "center", padding: "50px" }}>
          <Spin size="large" />
        </div>
      ) : errors.length > 0 ? (
        <Table
          columns={columns}
          dataSource={errors.map(error => ({ ...error, key: error.id }))}
          pagination={{ pageSize: 5, showSizeChanger: true, pageSizeOptions: ['5', '10', '20'], size: 'small' }}
          loading={loading && !reportModalVisible && !resolveModalVisible && !errorDetailsModalVisible && !resolutionNotesModalVisible} // Show table loading indicator if modals aren't blocking
          size="small"
          rowClassName={(record) => !record.resolved ? 'table-row-pending' : ''}
          scroll={{ x: 700 }} // Add horizontal scroll if content overflows
        />
      ) : (
        !loading && <Empty description={computerId ? "No errors reported for this computer" : "Select a computer to view errors"} />
      )}

      {/* --- Modals --- */}

      {/* Resolve Error Modal */}
      <Modal
        title="Resolve Error"
        open={resolveModalVisible}
        onOk={handleResolveSubmit}
        onCancel={() => setResolveModalVisible(false)}
        confirmLoading={loading}
        okText="Mark as Resolved"
        destroyOnClose
      >
        {currentError && (
          <>
            <div style={{ marginBottom: 16 }}>
              <Text strong>Error Type:</Text> {getErrorTypeTag(currentError.error_type)} <br />
              <Text strong>Message:</Text> {currentError.error_message} <br />
              <Text strong>Reported At:</Text> {new Date(currentError.reported_at).toLocaleString()}
            </div>
            <Form form={resolveForm} layout="vertical" preserve={false}>
              <Form.Item name="resolution_notes" label="Resolution Notes" rules={[{ required: true, message: "Please enter resolution notes" }]}>
                <TextArea rows={4} placeholder="Enter details about how this error was resolved" />
              </Form.Item>
            </Form>
          </>
        )}
      </Modal>

      {/* Report New Error Modal (Updated with Error Details) */}
      <Modal
        title="Report New Error"
        open={reportModalVisible}
        onOk={handleReportSubmit}
        onCancel={() => setReportModalVisible(false)}
        confirmLoading={loading}
        okText="Report Error"
        destroyOnClose
        width={600} // Increase width for details section
      >
         <Form form={reportForm} layout="vertical" preserve={false} initialValues={{ error_details_list: [{ key: '', value: '' }] }}> {/* Add initial empty item */}
           <Form.Item name="error_type" label="Error Type" rules={[{ required: true, message: "Please select the error type" }]}>
             <Select placeholder="Select error type">
               {STANDARDIZED_ERROR_TYPES.map(type => (<Option key={type} value={type}>{type}</Option>))}
             </Select>
           </Form.Item>
           <Form.Item name="error_message" label="Error Message" rules={[{ required: true, message: "Please describe the error" }]}>
             <TextArea rows={3} placeholder="Describe the error encountered" />
           </Form.Item>

           {/* Dynamic Error Details Section */}
           <Form.Item label="Error Details (Optional Key-Value Pairs)">
             <Form.List name="error_details_list">
               {(fields, { add, remove }) => (
                 <>
                   {fields.map(({ key, name, ...restField }) => (
                     <Space key={key} style={{ display: 'flex', marginBottom: 8 }} align="baseline">
                       <Form.Item
                         {...restField}
                         name={[name, 'key']}
                         // rules={[{ required: true, message: 'Missing key' }]} // Make key optional or required based on needs
                         style={{ marginBottom: 0 }}
                       >
                         <Input placeholder="Key (e.g., IP Address)" style={{ width: '180px' }}/>
                       </Form.Item>
                       <Form.Item
                         {...restField}
                         name={[name, 'value']}
                         // rules={[{ required: true, message: 'Missing value' }]} // Make value optional or required
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

       {/* Error Details Modal (New) */}
       <Modal
         title="Error Details"
         open={errorDetailsModalVisible}
         onCancel={() => setErrorDetailsModalVisible(false)}
         footer={[ <Button key="back" onClick={() => setErrorDetailsModalVisible(false)}>Close</Button> ]} // Only Close button needed
       >
         {currentError && (
           <>
             <Paragraph>
               <Text strong>Type:</Text> {getErrorTypeTag(currentError.error_type)}
             </Paragraph>
             <Paragraph>
               <Text strong>Reported At:</Text> {new Date(currentError.reported_at).toLocaleString()}
             </Paragraph>
              <Paragraph>
                 <Text strong>Message:</Text>
                 <Paragraph style={{ marginTop: '4px', paddingLeft: '10px', borderLeft: '3px solid #eee' }}>
                     {currentError.error_message || "N/A"}
                 </Paragraph>
             </Paragraph>
             <Paragraph>
               <Text strong>Additional Details:</Text>
               {renderErrorDetails(currentError.error_details)}
             </Paragraph>
           </>
         )}
       </Modal>

       {/* Resolution Notes Modal (New) */}
       <Modal
         title="Resolution Notes"
         open={resolutionNotesModalVisible}
         onCancel={() => setResolutionNotesModalVisible(false)}
         footer={[ <Button key="back" onClick={() => setResolutionNotesModalVisible(false)}>Close</Button> ]}
       >
         {currentError && (
           <>
             <Paragraph>
               <Text strong>Error Type:</Text> {getErrorTypeTag(currentError.error_type)}
             </Paragraph>
             <Paragraph>
                <Text strong>Resolved At:</Text> {currentError.resolved_at ? new Date(currentError.resolved_at).toLocaleString() : "N/A"}
             </Paragraph>
             <Paragraph>
               <Text strong>Notes:</Text>
                <Paragraph style={{ marginTop: '4px', paddingLeft: '10px', borderLeft: '3px solid #eee' }}>
                   {currentError.resolution_notes || "No resolution notes provided."}
                </Paragraph>
             </Paragraph>
           </>
         )}
       </Modal>

      {/* Optional Styling */}
      <style>{`
        .table-row-pending td { /* Optional: Highlight pending rows */
          /* background-color: #fffbe6 !important; */
        }
        .ant-table-thead > tr > th {
            background-color: #fafafa !important; /* Header background */
            font-weight: bold; /* Make header bold */
        }
        .ant-form-item {
            margin-bottom: 16px; /* Consistent spacing for form items */
        }
      `}</style>
    </div>
  );
};

export default ComputerError;
