/**
 * @fileoverview Room layout grid component
 * 
 * This component renders a room layout as a grid, displaying computers
 * positioned in their respective locations within a room.
 * 
 * @module RoomLayout
 */
import React from 'react';
import { Row, Col, Card, Empty, Badge } from 'antd';
import { DesktopOutlined } from '@ant-design/icons';
import SimpleComputerCard, { cardStyle } from '../computer/SimpleComputerCard';
import { LoadingComponent } from '../../components/common';

/**
 * RoomLayout Component
 * 
 * Renders a room's layout as a grid with computers positioned according to layout configuration.
 * Shows loading state or empty state when layout information is not available.
 *
 * @component
 * @param {Object} props - Component props
 * @param {Array<Object>} props.computers - Array of computer objects to display in the layout
 * @param {Object} props.room - Room object containing layout information
 * @returns {React.ReactElement} The rendered RoomLayout component
 */
const RoomLayout = ({ computers, room }) => {

  if (!computers || !room) {
    return <LoadingComponent type="inline" tip="Đang tải giao diện phòng..." />;
  }

  if (!room.layout) {
    return (
      <Empty description="Room layout information is not available" />
    );
  }

  // Get layout dimensions
  const rows = room.layout.rows || 4;
  const columns = room.layout.columns || 4;
  
  /**
   * Creates a 2D grid representation of the room layout
   * with computers positioned according to their coordinates
   * 
   * @function
   * @returns {React.ReactElement[]} Array of row elements containing computer cells
   */
  const renderGrid = () => {
    const grid = [];
    
    // For each row
    for (let y = 0; y < rows; y++) {
      const rowCells = [];
      
      // For each column in this row
      for (let x = 0; x < columns; x++) {
        // Find a computer at this position
        const computer = computers.find(comp => 
          comp.pos_x === x && comp.pos_y === y
        );
        
        rowCells.push(
          <Col span={24 / columns} key={`cell-${x}-${y}`} style={{ width: `${100/columns}%` }}>
            <div className="grid-cell" style={{ padding: '8px', width: '100%' }}>
              {computer ? (
                // Use the SimpleComputerCard component with simplified=true for consistent styling
                <SimpleComputerCard 
                  computer={computer}
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