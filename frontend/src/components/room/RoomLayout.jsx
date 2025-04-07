import React, { useState, useEffect } from 'react';
import { Row, Col, Card, Tooltip, Button, Popconfirm, message, Empty, Badge } from 'antd';
import { EditOutlined, DeleteOutlined, SettingOutlined, DesktopOutlined, GlobalOutlined, InfoCircleOutlined } from '@ant-design/icons';
import computerService from '../../services/computer.service';
import roomService from '../../services/room.service';
import { useAuth } from '../../contexts/AuthContext';
import ComputerCard, { cardStyle } from '../computer/ComputerCard';

const RoomLayout = ({ roomId, computers, onEditComputer, onViewComputer, onRefresh }) => {
  const [layoutData, setLayoutData] = useState([]);
  const [room, setRoom] = useState(null);
  const [loading, setLoading] = useState(true);
  const { isAdmin, hasRoomAccess } = useAuth();

  useEffect(() => {
    if (computers && computers.length) {
      setLayoutData(computers);
    }
    
    // Fetch room data to get layout information
    fetchRoomData();
  }, [computers, roomId]);

  const fetchRoomData = async () => {
    try {
      setLoading(true);
      const response = await roomService.getRoomById(roomId);
      const roomData = response.data;
      setRoom(roomData);
    } catch (error) {
      console.error('Error loading room data:', error);
    } finally {
      setLoading(false);
    }
  };

  // Check if user has access to this room
  const canAccessRoom = isAdmin || hasRoomAccess(roomId);

  if (loading) {
    return <div>Loading room layout...</div>;
  }

  if (!room || !room.layout) {
    return (
      <Empty description="Room layout information is not available" />
    );
  }

  // Get layout dimensions
  const rows = room.layout.rows || 4;
  const columns = room.layout.columns || 4;
  
  // Create a 2D grid representation
  const renderGrid = () => {
    const grid = [];
    
    // For each row
    for (let y = 0; y < rows; y++) {
      const rowCells = [];
      
      // For each column in this row
      for (let x = 0; x < columns; x++) {
        // Find a computer at this position
        const computer = layoutData.find(comp => 
          comp.pos_x === x && comp.pos_y === y
        );
        
        rowCells.push(
          <Col span={24 / columns} key={`cell-${x}-${y}`} style={{ width: `${100/columns}%` }}>
            <div className="grid-cell" style={{ padding: '8px', width: '100%' }}>
              {computer ? (
                // Use the ComputerCard component with simplified=true for consistent styling
                <ComputerCard 
                  computer={computer}
                  onEdit={onEditComputer}
                  onView={onViewComputer}
                  onRefresh={onRefresh}
                  simplified={true}
                />
              ) : (
                <Card 
                  hoverable
                  className="empty-cell-card" 
                  style={{
                    ...cardStyle,
                    height: '180px',
                    width: '100%',   // Ensure card takes up full width
                    maxWidth: '100%', // Prevent expansion beyond container
                    backgroundColor: '#fafafa',
                    borderStyle: 'dashed',
                    borderColor: '#d9d9d9'
                  }}
                  size="small"
                  title={
                    <div style={{ display: 'flex', alignItems: 'center', fontSize: '12px', width: '100%' }}>
                      <Badge status="default" style={{ fontSize: '10px' }} />
                      <span style={{ marginLeft: '4px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontSize: '12px' }}>
                        Empty
                      </span>
                    </div>
                  }
                  styles={{
                    body: { 
                      flex: 1, 
                      display: 'flex', 
                      alignItems: 'center', 
                      justifyContent: 'center',
                      padding: '8px',
                      width: '100%',
                      overflow: 'hidden'
                    },
                    header: {
                      padding: '0 12px',
                      minHeight: '32px',
                      fontSize: '12px',
                      width: '100%'
                    }
                  }}
                >
                  <div style={{ textAlign: 'center', width: '100%' }}>
                    <DesktopOutlined style={{ fontSize: '28px', color: '#d9d9d9' }} />
                    <p style={{ marginTop: '8px', color: '#999', margin: '4px 0', fontSize: '11px' }}>Position ({x}, {y})</p>
                  </div>
                </Card>
              )}
            </div>
          </Col>
        );
      }
      
      grid.push(
        <Row 
          gutter={[16, 16]} 
          key={`row-${y}`} 
          style={{ marginBottom: '16px', width: '100%' }}
          justify="space-between"
        >
          {rowCells}
        </Row>
      );
    }
    
    return grid;
  };

  return (
    <div className="room-layout" style={{ width: '100%' }}>
      <div style={{ marginBottom: '16px' }}>
        <p><strong>Room Layout:</strong> {rows} rows × {columns} columns</p>
      </div>
      
      <div className="layout-grid" style={{ width: '100%' }}>
        {renderGrid()}
      </div>
    </div>
  );
};

export default RoomLayout;