import React, { useEffect } from 'react';
import { Form, Input, Button, Select, InputNumber, message, Alert } from 'antd';

const { Option } = Select;

const CommonForm = ({ 
  type, // 'room' hoặc 'user'
  initialValues, 
  onSuccess, 
  onCancel,
  service // service object để gọi API
}) => {
  const [form] = Form.useForm();
  const isEditing = !!initialValues?.id;

  useEffect(() => {
    if (initialValues) {
      if (type === 'user') {
        form.setFieldsValue({
          ...initialValues,
          role: initialValues.role || (initialValues.roles && initialValues.roles[0]) || 'user',
          is_active: initialValues.is_active !== undefined ? initialValues.is_active : 
                    (initialValues.status === 'active')
        });
      } else if (type === 'room') {
        form.setFieldsValue({
          name: initialValues.name,
          description: initialValues.description,
          rows: initialValues.layout?.rows || 4,
          columns: initialValues.layout?.columns || 4,
        });
      }
    }
  }, [initialValues, form, type]);

  const handleSubmit = async (values) => {
    try {
      if (type === 'user') {
        if (isEditing) {
          const { ...updateData } = values;
          await service.updateUser(initialValues.id, updateData);
        } else {
          await service.createUser(values);
        }
      } else if (type === 'room') {
        const roomData = {
          name: values.name,
          description: values.description,
          layout: {
            rows: values.rows,
            columns: values.columns
          }
        };

        if (isEditing) {
          await service.updateRoom(initialValues.id, roomData);
        } else {
          await service.createRoom(roomData);
        }
      }
      
      form.resetFields();
      if (onSuccess) onSuccess();
      message.success(`${type === 'user' ? 'User' : 'Room'} ${isEditing ? 'updated' : 'created'} successfully`);
    } catch (error) {
      const errorMsg = error.response?.data?.message || error.message || 
        `Failed to ${isEditing ? 'update' : 'create'} ${type}`;
      message.error(errorMsg);
      console.error(`Error ${isEditing ? 'updating' : 'creating'} ${type}:`, error);
    }
  };

  const renderUserForm = () => (
    <>
      {isEditing && (
        <Alert
          message="Note"
          description="Username and password cannot be changed in this form. Use a separate process for password resets."
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}
      
      <Form.Item
        name="username"
        label="Username"
        rules={[{ required: !isEditing, message: 'Please enter username' }]}
      >
        <Input 
          placeholder="Enter username" 
          disabled={isEditing} 
        />
      </Form.Item>

      {!isEditing && (
        <Form.Item
          name="password"
          label="Password"
          rules={[{ required: !isEditing, message: 'Please enter password' }]}
        >
          <Input.Password placeholder="Enter password" />
        </Form.Item>
      )}

      <Form.Item
        name="role"
        label="Role"
        rules={[{ required: true, message: 'Please select a role' }]}
      >
        <Select placeholder="Select role">
          <Option value="admin">Administrator</Option>
          <Option value="user">Regular User</Option>
        </Select>
      </Form.Item>

      <Form.Item
        name="is_active"
        label="Status"
        valuePropName="checked"
      >
        <Select placeholder="Select status">
          <Option value={true}>Active</Option>
          <Option value={false}>Inactive</Option>
        </Select>
      </Form.Item>
    </>
  );

  const renderRoomForm = () => (
    <>
      <Form.Item
        name="name"
        label="Room Name"
        rules={[{ required: true, message: 'Please enter room name' }]}
      >
        <Input placeholder="Enter room name (e.g., Lab Room 101)" />
      </Form.Item>

      <Form.Item
        name="description"
        label="Description"
        rules={[{ required: true, message: 'Please enter room description' }]}
      >
        <Input.TextArea rows={4} placeholder="Enter room description" />
      </Form.Item>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Form.Item
          name="columns"
          label="Columns (X-axis)"
          tooltip="Number of computers horizontally (X-axis)"
          rules={[{ required: true, message: 'Please enter number of columns' }]}
        >
          <InputNumber min={1} max={20} style={{ width: '100%' }} disabled={isEditing} />
        </Form.Item>

        <Form.Item
          name="rows"
          label="Rows (Y-axis)"
          tooltip="Number of computers vertically (Y-axis)"
          rules={[{ required: true, message: 'Please enter number of rows' }]}
        >
          <InputNumber min={1} max={20} style={{ width: '100%' }} disabled={isEditing} />
        </Form.Item>
      </div>
      
      {isEditing && (
        <div style={{ marginBottom: '16px', backgroundColor: '#f6ffed', padding: '10px', border: '1px solid #b7eb8f', borderRadius: '4px' }}>
          <strong>Note:</strong> Layout dimensions cannot be modified after room creation.
        </div>
      )}
    </>
  );

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleSubmit}
      initialValues={type === 'user' ? { role: 'user', is_active: true } : { columns: 4, rows: 4 }}
    >
      {type === 'user' ? renderUserForm() : renderRoomForm()}

      <Form.Item>
        <Button type="primary" htmlType="submit">
          {isEditing ? `Update ${type === 'user' ? 'User' : 'Room'}` : `Create ${type === 'user' ? 'User' : 'Room'}`}
        </Button>
        <Button 
          style={{ marginLeft: 8 }} 
          onClick={onCancel}
        >
          Cancel
        </Button>
      </Form.Item>
    </Form>
  );
};

export default CommonForm; 