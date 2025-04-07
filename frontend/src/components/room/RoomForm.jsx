import React, { useEffect } from 'react';
import { Form, Input, Button, InputNumber, message } from 'antd';
import roomService from '../../services/room.service';

const RoomForm = ({ initialValues, onSuccess, onCancel }) => {
  const [form] = Form.useForm();
  const isEditing = !!initialValues?.id;

  useEffect(() => {
    if (initialValues) {
      // Format the initialValues to match the form structure
      form.setFieldsValue({
        ...initialValues,
        rows: initialValues.layout?.rows || 4,
        columns: initialValues.layout?.columns || 4,
      });
    }
  }, [initialValues, form]);

  const handleSubmit = async (values) => {
    try {
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
        message.success('Room updated successfully');
      } else {
        await roomService.createRoom(roomData);
        message.success('Room created successfully');
      }
      form.resetFields();
      if (onSuccess) onSuccess();
    } catch (error) {
      message.error(`Failed to ${isEditing ? 'update' : 'create'} room`);
      console.error(`Error ${isEditing ? 'updating' : 'creating'} room:`, error);
    }
  };

  return (
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
          <InputNumber min={1} max={20} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          name="rows"
          label="Rows (Y-axis)"
          tooltip="Number of computers vertically (Y-axis)"
          rules={[{ required: true, message: 'Please enter number of rows' }]}
        >
          <InputNumber min={1} max={20} style={{ width: '100%' }} />
        </Form.Item>
      </div>

      <Form.Item>
        <Button type="primary" htmlType="submit">
          {isEditing ? 'Update Room' : 'Create Room'}
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

export default RoomForm;