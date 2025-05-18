import React, { useState, useEffect, useMemo } from 'react';
import { Transfer, Button, message } from 'antd';
import { LoadingComponent } from '../common';
import {
  useAppDispatch,
  useAppSelector,
  fetchUsers,
  selectUsers,
  selectUserLoading,
  fetchUserRooms,
  updateUserRooms,
  selectUserRooms
} from '../../app/index';

const AssignmentComponent = ({ type, id, onSuccess }) => {
  const dispatch = useAppDispatch();
  const [targetKeys, setTargetKeys] = useState([]);
  const [initialAssignments, setInitialAssignments] = useState([]);
  const [actionLoading, setActionLoading] = useState(false);

  const users = useAppSelector(selectUsers);
  const userRooms = useAppSelector(selectUserRooms);
  const loading = useAppSelector(selectUserLoading);

  useEffect(() => {
    if (type === 'room') {
      dispatch(fetchUsers());
      if (id) {
        dispatch(fetchUserRooms(id));
      }
    }
  }, [dispatch, type, id]);

  const availableItems = useMemo(() => {
    return (users || []).map(user => ({
      key: user.id.toString(),
      title: `${user.firstName || ''} ${user.lastName || ''} (${user.username || 'User'})`.trim(),
      description: user.email || '',
    }));
  }, [users]);

  useEffect(() => {
    if (userRooms) {
      const assignedKeys = (userRooms || []).map(item => item.id.toString());
      setTargetKeys(assignedKeys);
      setInitialAssignments(assignedKeys);
    }
  }, [userRooms]);

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
        await dispatch(updateUserRooms({ roomId: id, addUsers: itemsToAdd, removeUsers: itemsToRemove })).unwrap();
        await dispatch(fetchUserRooms(id));
      } else {
        success = false;
        message.warn(`Save logic for type '${type}' not implemented.`);
      }

      if (success) {
        message.success('Assignments updated successfully');
        if (onSuccess) onSuccess(userRooms || []);
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
        <LoadingComponent type="section" tip="Loading assignment data..." />
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