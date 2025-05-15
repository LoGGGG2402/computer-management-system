import React, { useState, useEffect } from 'react';
import { 
  Table, Button, Form, Input, Upload, Card, Typography, Space, 
  Tag, Popconfirm, message, Tooltip, Switch, Select
} from 'antd';
import { 
  UploadOutlined, CheckCircleOutlined, 
  ExclamationCircleOutlined, InfoCircleOutlined 
} from '@ant-design/icons';
import adminService from '../../services/admin.service';

const { Title, Text } = Typography;
const { TextArea } = Input;
const { Option } = Select;

/**
 * Calculate SHA-256 checksum of file
 * @param {File} file - File to calculate checksum
 * @returns {Promise<string>} - SHA-256 checksum in hex format
 */
const calculateSHA256 = async (file) => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    
    reader.onload = async (e) => {
      try {
        const buffer = e.target.result;
        const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
        resolve(hashHex);
      } catch (error) {
        reject(error);
      }
    };
    
    reader.onerror = (error) => reject(error);
    reader.readAsArrayBuffer(file);
  });
};

/**
 * Agent Version Management component
 * 
 * This component provides an interface for administrators to:
 * - View all agent versions
 * - Upload new agent versions
 * - Mark specific agent versions as stable
 */
const AgentVersionManagement = () => {
  const [versions, setVersions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [uploadLoading, setUploadLoading] = useState(false);
  const [fileChecksum, setFileChecksum] = useState('');
  const [form] = Form.useForm();

  // Load agent versions on component mount
  useEffect(() => {
    fetchVersions();
  }, []);

  /**
   * Fetch agent versions from the server
   */
  const fetchVersions = async () => {
    setLoading(true);
    try {
      const data = await adminService.getAgentVersions();
      setVersions(data);
    } catch (error) {
      message.error(`Failed to load agent versions: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  /**
   * Mark a version as stable
   * @param {string} versionId - ID of the version to mark as stable
   */
  const handleMarkStable = async (versionId) => {
    try {
      await adminService.updateAgentVersionStability(versionId, true);
      message.success('Agent version marked as stable successfully');
      fetchVersions();
    } catch (error) {
      message.error(`Failed to mark version as stable: ${error.message}`);
    }
  };

  /**
   * Handle file change to calculate checksum
   */
  const handleFileChange = async (info) => {
    const file = info.file.originFileObj;
    if (file) {
      try {
        const checksum = await calculateSHA256(file);
        setFileChecksum(checksum);
        // Show checksum preview
        message.info(`File checksum calculated: ${checksum.substring(0, 8)}...${checksum.substring(checksum.length - 8)}`);
      } catch (error) {
        console.error('Error calculating checksum:', error);
        message.error('Failed to calculate file checksum');
        setFileChecksum('');
      }
    } else {
      setFileChecksum('');
    }
  };

  /**
   * Handle form submission for uploading a new agent version
   * @param {Object} values - Form values
   */
  const handleUpload = async (values) => {
    if (!values.file || !values.file.fileList || !values.file.fileList.length) {
      message.error('Please select a file to upload');
      return;
    }

    if (!fileChecksum) {
      message.error('File checksum is missing');
      return;
    }

    // Create FormData for file upload
    const formData = new FormData();
    formData.append('package', values.file.fileList[0].originFileObj);
    formData.append('version', values.version);
    formData.append('checksum', fileChecksum);
    
    if (values.notes) {
      formData.append('notes', values.notes);
    }

    setUploadLoading(true);
    try {
      await adminService.uploadAgentPackage(formData);
      message.success('Agent version uploaded successfully');
      form.resetFields();
      setFileChecksum('');
      fetchVersions();
    } catch (error) {
      message.error(`Failed to upload agent version: ${error.message}`);
    } finally {
      setUploadLoading(false);
    }
  };

  /**
   * Format file size to human-readable string
   * @param {number} bytes - File size in bytes
   * @returns {string} Formatted file size
   */
  const formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  /**
   * Format date to readable string
   * @param {string} dateString - ISO date string
   * @returns {string} Formatted date
   */
  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  /**
   * Table columns definition
   */
  const columns = [
    {
      title: 'Version',
      dataIndex: 'version',
      key: 'version',
      render: (text, record) => (
        <Space>
          {text}
          {record.is_stable && (
            <Tag color="green">Stable</Tag>
          )}
        </Space>
      )
    },
    {
      title: 'Checksum (SHA-256)',
      dataIndex: 'checksum_sha256',
      key: 'checksum_sha256',
      render: (text) => (
        <Tooltip title={text}>
          <span className="text-xs font-mono">{text.substring(0, 16)}...</span>
        </Tooltip>
      )
    },
    {
      title: 'Size',
      dataIndex: 'file_size',
      key: 'file_size',
      render: (size) => formatFileSize(size)
    },
    {
      title: 'Notes',
      dataIndex: 'notes',
      key: 'notes',
      render: (notes) => notes || '-'
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space>
          <a 
            href={`${import.meta.env.VITE_API_URL || ''}${record.download_url}`} 
            target="_blank" 
            rel="noopener noreferrer"
          >
            Download
          </a>
          {!record.is_stable && (
            <Popconfirm
              title="Mark this version as stable?"
              description="This will make all other versions non-stable."
              onConfirm={() => handleMarkStable(record.id)}
              okText="Yes"
              cancelText="No"
            >
              <Button type="primary" size="small">Mark Stable</Button>
            </Popconfirm>
          )}
        </Space>
      )
    },
  ];

  return (
    <div className="agent-version-management">
      <Title level={2}>Agent Version Management</Title>
      
      {/* Upload Form */}
      <Card title="Upload New Agent Version" className="mb-6">
        <Form
          form={form}
          layout="vertical"
          onFinish={handleUpload}
        >
          <Form.Item
            label="Version"
            name="version"
            rules={[
              { required: true, message: 'Please enter version number' },
              { 
                pattern: /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+)?$/, 
                message: 'Please enter a valid semantic version (e.g., 1.0.0)'
              }
            ]}
          >
            <Input placeholder="e.g., 1.0.0" />
          </Form.Item>
          
          <Form.Item
            label="Agent Package File"
            name="file"
            rules={[{ required: true, message: 'Please upload agent package file' }]}
          >
            <Upload
              maxCount={1}
              beforeUpload={() => false}
              accept=".zip"
              onChange={handleFileChange}
            >
              <Button icon={<UploadOutlined />}>Select Agent Package</Button>
            </Upload>
          </Form.Item>
          
          {fileChecksum && (
            <Form.Item
              label="File Checksum (SHA-256)"
            >
              <Input 
                value={fileChecksum} 
                readOnly 
                className="font-mono text-xs"
                addonAfter={<CheckCircleOutlined style={{ color: 'green' }} />}
              />
            </Form.Item>
          )}
          
          <Form.Item
            label="Release Notes"
            name="notes"
          >
            <TextArea rows={4} placeholder="Describe what's new in this version" />
          </Form.Item>
          
          <Form.Item>
            <Button 
              type="primary" 
              htmlType="submit" 
              loading={uploadLoading}
              disabled={uploadLoading || !fileChecksum}
            >
              Upload Version
            </Button>
          </Form.Item>
        </Form>
      </Card>
      
      {/* Versions Table */}
      <Card title="Agent Versions">
        <div className="flex justify-between mb-4">
          <Text>
            <InfoCircleOutlined className="mr-2" />
            Manage agent versions and mark stable releases
          </Text>
          <Button onClick={fetchVersions} loading={loading}>Refresh</Button>
        </div>
        
        <Table
          columns={columns}
          dataSource={versions}
          rowKey="id"
          loading={loading}
          pagination={{ pageSize: 10 }}
        />
      </Card>
    </div>
  );
};

export default AgentVersionManagement;