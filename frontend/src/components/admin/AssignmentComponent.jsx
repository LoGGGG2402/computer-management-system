import React, { useState, useEffect } from 'react';
import { Transfer, Button, message } from 'antd';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';
import { LoadingComponent } from '../common';

const AssignmentComponent = ({ type, id, onSuccess }) => {
  const [targetKeys, setTargetKeys] = useState([]);
  const [availableItems, setAvailableItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [initialAssignments, setInitialAssignments] = useState([]);
  const [assignedUsers, setAssignedUsers] = useState([]);

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
        const [allUsers, assignedRoomUsers] = await Promise.all([
          userService.getAllUsers(),
          roomService.getUsersInRoom(id)
        ]);
        
        // Format all users for the Transfer component
        const formattedUsers = allUsers?.users?.map(user => ({
          key: user.id,
          title: `${user.firstName || ''} ${user.lastName || ''} (${user.username || 'User'})`,
          description: user.email || '',
        })) || [];
        
        setAvailableItems(formattedUsers);
        
        // Extract assigned user IDs
        const assignedUserIds = assignedRoomUsers?.map(user => user.id) || [];
        setAssignedUsers(assignedRoomUsers || []);
        
        setTargetKeys(assignedUserIds);
        setInitialAssignments(assignedUserIds);
      } else if (type === 'user') {
        // For user-to-room assignments, we'd need similar functionality
        // But it appears the user service doesn't have a getUserRooms method
        // This would need to be implemented in the backend and user service
        message.info('User-to-Room assignment functionality is not implemented yet');
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
        let updatedUsers = [...assignedUsers];
        
        if (itemsToAdd.length > 0) {
          await roomService.assignUsersToRoom(id, itemsToAdd);
          // We'd need to fetch the updated user data to get complete user objects
        }
        
        if (itemsToRemove.length > 0) {
          await roomService.unassignUsersFromRoom(id, itemsToRemove);
          updatedUsers = updatedUsers.filter(user => !itemsToRemove.includes(user.id));
        }
        
        // If any changes were made, fetch the updated list of users
        if (itemsToAdd.length > 0 || itemsToRemove.length > 0) {
          const refreshedUsers = await roomService.getUsersInRoom(id);
          setAssignedUsers(refreshedUsers);
          
          // Update initial assignments to match current state after successful save
          const refreshedUserIds = refreshedUsers.map(user => user.id);
          setTargetKeys(refreshedUserIds);
          setInitialAssignments(refreshedUserIds);
        }
      }
      
      message.success('Assignments updated successfully');
      if (onSuccess) onSuccess(assignedUsers);
    } catch (error) {
      message.error('Failed to update assignments');
      console.error('Error updating assignments:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="assignment-component">
      {loading ? (
        <LoadingComponent type="section" tip="Loading assignment data..." />
      ) : (
        <>
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
        </>
      )}
    </div>
  );
};

export default AssignmentComponent;