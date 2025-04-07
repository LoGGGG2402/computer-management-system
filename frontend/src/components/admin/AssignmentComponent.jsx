import React, { useState, useEffect } from 'react';
import { Transfer, Button, message, Spin } from 'antd';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';

const AssignmentComponent = ({ type, id, onSuccess }) => {
  const [targetKeys, setTargetKeys] = useState([]);
  const [availableItems, setAvailableItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [initialAssignments, setInitialAssignments] = useState([]);

  useEffect(() => {
    if (id) {
      fetchData();
    }
  }, [id, type]);

  const fetchData = async () => {
    try {
      setLoading(true);
      if (type === 'room') {
        // For a room, we need all users and the users assigned to this room
        const [allUsersResponse, roomUsersResponse] = await Promise.all([
          userService.getAllUsers(),
          roomService.getUsersInRoom(id)
        ]);
        
        // Extract users from potentially nested response
        const allUsers = Array.isArray(allUsersResponse) 
          ? allUsersResponse 
          : (allUsersResponse?.data?.users || []);
        
        // Extract room users from potentially nested response
        const roomUsers = Array.isArray(roomUsersResponse) 
          ? roomUsersResponse 
          : (roomUsersResponse?.data?.users || []);

        
        // Check if allUsers is an array before mapping
        const formattedUsers = Array.isArray(allUsers) 
          ? allUsers.map(user => ({
              key: user.id,
              title: `${user.firstName || ''} ${user.lastName || ''} (${user.username || 'User'})`,
              description: user.email || '',
            }))
          : [];
        
        setAvailableItems(formattedUsers);
        
        // Check if roomUsers is an array before mapping
        const roomUserIds = Array.isArray(roomUsers)
          ? roomUsers.map(user => user.id)
          : [];
        
        setTargetKeys(roomUserIds);
        setInitialAssignments(roomUserIds);
      } else if (type === 'user') {
        // For a user, we need all rooms and the rooms this user is assigned to
        const [allRoomsResponse, userRoomsResponse] = await Promise.all([
          roomService.getAllRooms(),
          userService.getUserRooms(id)
        ]);
        
        // Extract rooms from potentially nested response
        const allRooms = Array.isArray(allRoomsResponse) 
          ? allRoomsResponse 
          : (allRoomsResponse?.data?.rooms || []);
        
        // Extract user's rooms from potentially nested response
        const userRooms = Array.isArray(userRoomsResponse) 
          ? userRoomsResponse 
          : (userRoomsResponse?.data?.rooms || []);
      
        
        // Check if allRooms is an array before mapping
        const formattedRooms = Array.isArray(allRooms) 
          ? allRooms.map(room => ({
              key: room.id,
              title: `${room.name || 'Room'}`,
              description: room.description || '',
            }))
          : [];
        
        setAvailableItems(formattedRooms);
        
        // Check if userRooms is an array before mapping
        const userRoomIds = Array.isArray(userRooms)
          ? userRooms.map(room => room.id)
          : [];
        
        setTargetKeys(userRoomIds);
        setInitialAssignments(userRoomIds);
      }
    } catch (error) {
      message.error('Failed to load assignment data');
      console.error('Error loading assignment data:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (newTargetKeys) => {
    setTargetKeys(newTargetKeys);
  };

  const handleSave = async () => {
    try {
      setLoading(true);
      
      // Find items to add (in targetKeys but not in initialAssignments)
      const itemsToAdd = targetKeys.filter(key => !initialAssignments.includes(key));
      
      // Find items to remove (in initialAssignments but not in targetKeys)
      const itemsToRemove = initialAssignments.filter(key => !targetKeys.includes(key));
    
      
      if (type === 'room') {
        // For a room, we're assigning/unassigning users
        if (itemsToAdd.length > 0) {
          await roomService.assignUsersToRoom(id, itemsToAdd);
        }
        if (itemsToRemove.length > 0) {
          await roomService.unassignUsersFromRoom(id, itemsToRemove);
        }
      } else if (type === 'user') {
        // Here we would use equivalent service methods for assigning rooms to users
        // This requires implementing these methods in the user service
        message.info('User-Room assignment functionality is being implemented');
      }
      
      // Update initial assignments to match current state after successful save
      setInitialAssignments([...targetKeys]);
      
      message.success('Assignments updated successfully');
      if (onSuccess) onSuccess();
    } catch (error) {
      message.error('Failed to update assignments');
      console.error('Error updating assignments:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="assignment-component">
      <Spin spinning={loading}>
        <Transfer
          dataSource={availableItems}
          titles={[
            type === 'room' ? 'Available Users' : 'Available Rooms',
            type === 'room' ? 'Assigned Users' : 'Assigned Rooms'
          ]}
          targetKeys={targetKeys}
          onChange={handleChange}
          render={item => item.title}
          listStyle={{
            width: 350,
            height: 300,
          }}
        />
        <div style={{ marginTop: 16, textAlign: 'right' }}>
          <Button type="primary" onClick={handleSave}>
            Save Assignments
          </Button>
        </div>
      </Spin>
    </div>
  );
};

export default AssignmentComponent;