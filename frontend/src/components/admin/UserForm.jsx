import React, { useEffect } from 'react';
import { Form, Input, Button, Select, message, Alert } from 'antd';
import userService from '../../services/user.service';

const { Option } = Select;

const UserForm = ({ initialValues, onSuccess, onCancel }) => {
  const [form] = Form.useForm();
  const isEditing = !!initialValues?.id;

  useEffect(() => {
    if (initialValues) {
      form.setFieldsValue({
        ...initialValues,
        // Convert roles array to single role value if needed
        role: initialValues.role || (initialValues.roles && initialValues.roles[0]) || 'user',
        is_active: initialValues.is_active !== undefined ? initialValues.is_active : 
                  (initialValues.status === 'active')
      });
    }
  }, [initialValues, form]);

  const handleSubmit = async (values) => {
    try {
      if (isEditing) {
        // When editing, don't send username or password fields
        const { username, password, ...updateData } = values;
        await userService.updateUser(initialValues.id, updateData);
        message.success('User updated successfully');
      } else {
        await userService.createUser(values);
        message.success('User created successfully');
      }
      form.resetFields();
      if (onSuccess) onSuccess();
    } catch (error) {
      const errorMsg = error.response?.data?.message || error.message || `Failed to ${isEditing ? 'update' : 'create'} user`;
      message.error(errorMsg);
      console.error(`Error ${isEditing ? 'updating' : 'creating'} user:`, error);
    }
  };

  return (
    <div className="user-form">
      {isEditing && (
        <Alert
          message="Note"
          description="Username and password cannot be changed in this form. Use a separate process for password resets."
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}
      
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        initialValues={initialValues || { role: 'user', is_active: true }}
      >
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

        <Form.Item>
          <Button type="primary" htmlType="submit">
            {isEditing ? 'Update User' : 'Create User'}
          </Button>
          <Button 
            style={{ marginLeft: 8 }} 
            onClick={onCancel}
          >
            Cancel
          </Button>
        </Form.Item>
      </Form>
    </div>
  );
};

export default UserForm;