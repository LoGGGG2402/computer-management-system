import React, { useEffect, useState } from 'react';
import { Form, Input, Button, Select, InputNumber, message, Alert } from 'antd';
import computerService from '../../services/computer.service';
import roomService from '../../services/room.service';

const { Option } = Select;

const ComputerForm = ({ initialValues, onSuccess, onCancel }) => {
  const [form] = Form.useForm();
  const [rooms, setRooms] = useState([]);
  const [loading, setLoading] = useState(false);
  
  const isEditing = !!initialValues?.id;

  useEffect(() => {
    fetchRooms();
    
    if (initialValues) {
      // Set form values from initial values
      form.setFieldsValue({
        ...initialValues,
        // Handle both room_id as a number and room as an object
        room_id: initialValues.room_id || (initialValues.room && initialValues.room.id)
      });
    }
  }, [initialValues, form]);

  const fetchRooms = async () => {
    try {
      setLoading(true);
      const response = await roomService.getAllRooms();
      
      let roomsData = [];
      if (response?.data?.rooms && Array.isArray(response.data.rooms)) {
        roomsData = response.data.rooms;
      } else if (response?.data && Array.isArray(response.data)) {
        roomsData = response.data;
      } else if (response?.rooms && Array.isArray(response.rooms)) {
        roomsData = response.rooms;
      } else if (Array.isArray(response)) {
        roomsData = response;
      }
      
      setRooms(roomsData || []);
    } catch (error) {
      console.error('Error fetching rooms:', error);
      message.error('Failed to load rooms');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (values) => {
    try {
      if (isEditing) {
        // When editing, don't send ip_address field
        const { ip_address, ...updateData } = values;
        await computerService.updateComputer(initialValues.id, updateData);
        message.success('Computer updated successfully');
      } else {
        await computerService.createComputer(values);
        message.success('Computer created successfully');
      }
      
      form.resetFields();
      if (onSuccess) onSuccess();
    } catch (error) {
      const errorMsg = error.response?.data?.message || error.message || `Failed to ${isEditing ? 'update' : 'create'} computer`;
      message.error(errorMsg);
      console.error(`Error ${isEditing ? 'updating' : 'creating'} computer:`, error);
    }
  };

  return (
    <div className="computer-form">
      {isEditing && (
        <Alert
          message="IP Address Restriction"
          description="IP address cannot be edited through this form. IP addresses are updated automatically by the agent software."
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}
      
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        disabled={loading}
      >
        <Form.Item
          name="name"
          label="Computer Name"
          rules={[{ required: true, message: 'Please enter computer name' }]}
        >
          <Input placeholder="Enter computer name" />
        </Form.Item>

        <Form.Item
          name="room_id"
          label="Room"
          rules={[{ required: true, message: 'Please select a room' }]}
        >
          <Select 
            placeholder="Select room" 
            loading={loading}
            showSearch
            optionFilterProp="children"
          >
            {rooms.map(room => (
              <Option key={room.id} value={room.id}>
                {room.name}
              </Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item
          name="ip_address"
          label="IP Address"
          rules={[
            { 
              pattern: /^(\d{1,3}\.){3}\d{1,3}$/, 
              message: 'Please enter a valid IP address'
            }
          ]}
        >
          <Input 
            placeholder="Enter IP address (optional)" 
            disabled={isEditing}
          />
        </Form.Item>

        <Form.Item
          name="pos_x"
          label="Position X"
          rules={[{ type: 'number' }]}
        >
          <InputNumber placeholder="X coordinate" style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          name="pos_y"
          label="Position Y"
          rules={[{ type: 'number' }]}
        >
          <InputNumber placeholder="Y coordinate" style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit">
            {isEditing ? 'Update Computer' : 'Create Computer'}
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

export default ComputerForm;