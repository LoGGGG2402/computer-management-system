import React, { useEffect, useState } from 'react';
import { Form, Input, Button, InputNumber, message, Spin } from 'antd';
import roomService from '../../services/room.service';

const RoomForm = ({ initialValues, onSuccess, onCancel }) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const isEditing = !!initialValues?.id;

  useEffect(() => {
    if (initialValues) {
      // Format the initialValues to match the form structure
      form.setFieldsValue({
        name: initialValues.name,
        description: initialValues.description,
        rows: initialValues.layout?.rows || 4,
        columns: initialValues.layout?.columns || 4,
      });
    } else {
      // Reset form when creating a new room
      form.resetFields();
    }
  }, [initialValues, form]);

  const handleSubmit = async (values) => {
    try {
      setLoading(true);
      
      // Transform form values to match the API expected structure
      const roomData = {
        name: values.name,
        description: values.description,
        layout: {
          rows: values.rows,
          columns: values.columns
        }
      };

      if (isEditing) {
        await roomService.updateRoom(initialValues.id, roomData);
      } else {
        await roomService.createRoom(roomData);
      }
      
      form.resetFields();
      if (onSuccess) onSuccess();
    } catch (error) {
      const errorMessage = error.message || `Failed to ${isEditing ? 'update' : 'create'} room`;
      message.error(errorMessage);
      console.error(`Error ${isEditing ? 'updating' : 'creating'} room:`, error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Spin spinning={loading} tip={isEditing ? "Updating room..." : "Creating room..."}>
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        initialValues={{
          name: '',
          description: '',
          columns: 4,
          rows: 4
        }}
      >
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

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={loading}>
            {isEditing ? 'Update Room' : 'Create Room'}
          </Button>
          <Button 
            style={{ marginLeft: 8 }} 
            onClick={onCancel}
            disabled={loading}
          >
            Cancel
          </Button>
        </Form.Item>
      </Form>
    </Spin>
  );
};

export default RoomForm;