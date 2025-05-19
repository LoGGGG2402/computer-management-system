import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Transfer, Button, message } from 'antd';
import roomService from '../../services/room.service';
import userService from '../../services/user.service';
import { Loading } from '../common';
import { useSimpleFetch } from '../../hooks/useSimpleFetch';

const AssignmentComponent = ({ type, id, onSuccess }) => {
  const [targetKeys, setTargetKeys] = useState([]);
  const [initialAssignments, setInitialAssignments] = useState([]);
  const [actionLoading, setActionLoading] = useState(false);

  const fetchAvailableItems = useCallback(async () => {
    if (type === 'room') {
      return await userService.getAllUsers();
    }
    return null;
  }, [type]);

  const { data: availableItemsData, loading: loadingAvailable } = useSimpleFetch(
    fetchAvailableItems,
    [fetchAvailableItems],
    { errorMessage: `Failed to load available ${type === 'room' ? 'users' : 'items'}` }
  );

  const fetchAssignedItems = useCallback(async () => {
    if (!id) return null;
    if (type === 'room') {
      return await roomService.getUsersInRoom(id);
    }
    return null;
  }, [id, type]);

  const { data: assignedItemsData, loading: loadingAssigned, refresh: refreshAssigned } = useSimpleFetch(
    fetchAssignedItems,
    [fetchAssignedItems],
    { errorMessage: `Failed to load assigned ${type === 'room' ? 'users' : 'items'}` }
  );

  const availableItems = useMemo(() => {
    const users = availableItemsData?.users || availableItemsData?.data?.users || availableItemsData?.data || availableItemsData || [];
    return users.map(user => ({
      key: user.id.toString(),
      title: `${user.firstName || ''} ${user.lastName || ''} (${user.username || 'User'})`.trim(),
      description: user.email || '',
    })) || [];
  }, [availableItemsData]);

  useEffect(() => {
    if (assignedItemsData) {
      const assignedKeys = (assignedItemsData || []).map(item => item.id.toString());
      setTargetKeys(assignedKeys);
      setInitialAssignments(assignedKeys);
    }
  }, [assignedItemsData]);

  const loading = loadingAvailable || loadingAssigned;

  const handleChange = (newTargetKeys) => {
    setTargetKeys(newTargetKeys);
  };

  const handleSave = async () => {
    setActionLoading(true);
    try {
      const itemsToAdd = targetKeys.filter(key => !initialAssignments.includes(key)).map(key => parseInt(key));
      const itemsToRemove = initialAssignments.filter(key => !targetKeys.includes(key)).map(key => parseInt(key));

      let success = true;
      if (type === 'room') {
        const promises = [];
        if (itemsToAdd.length > 0) {
          promises.push(roomService.assignUsersToRoom(id, itemsToAdd));
        }
        if (itemsToRemove.length > 0) {
          promises.push(roomService.unassignUsersFromRoom(id, itemsToRemove));
        }
        await Promise.all(promises);
      } else {
        success = false;
        message.warn(`Save logic for type '${type}' not implemented.`);
      }

      if (success) {
        message.success('Assignments updated successfully');
        const refreshedAssigned = await refreshAssigned();
        const refreshedKeys = (refreshedAssigned || []).map(item => item.id.toString());
        setInitialAssignments(refreshedKeys);
        setTargetKeys(refreshedKeys);

        if (onSuccess) onSuccess(refreshedAssigned || []);
      }
    } catch (error) {
      message.error('Failed to update assignments');
      console.error('Error updating assignments:', error);
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="assignment-component">
      {loading ? (
        <Loading type="section" tip="Loading assignment data..." />
      ) : (
        <>
          <Transfer
            dataSource={availableItems}
            titles={[
              type === 'room' ? 'Available Users' : 'Available Items',
              type === 'room' ? 'Assigned Users' : 'Assigned Items'
            ]}
            targetKeys={targetKeys}
            onChange={handleChange}
            render={item => item.title}
            listStyle={{
              width: '45%',
              height: 300,
            }}
            showSearch
            filterOption={(inputValue, item) =>
              item.title.toLowerCase().includes(inputValue.toLowerCase()) ||
              item.description.toLowerCase().includes(inputValue.toLowerCase())
            }
            disabled={actionLoading}
          />
          <div style={{ marginTop: 16, textAlign: 'right' }}>
            <Button type="primary" onClick={handleSave} loading={actionLoading} disabled={loading}>
              Save Assignments
            </Button>
          </div>
        </>
      )}
    </div>
  );
};

export default AssignmentComponent;