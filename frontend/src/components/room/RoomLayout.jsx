/**
 * @fileoverview Room layout grid component
 * 
 * This component renders a room layout as a grid, displaying computers
 * positioned in their respective locations within a room.
 * 
 * @module RoomLayout
 */
import React, { useEffect } from "react";
import { Row, Col, Card, Empty, Badge, Spin } from 'antd';
import { DesktopOutlined } from '@ant-design/icons';
import ComputerCard from './ComputerCard';
import {
  useAppDispatch,
  useAppSelector,
  fetchRoomLayout,
  fetchRoomComputers,
  selectRoomLayout,
  selectRoomComputers,
  selectRoomLayoutLoading,
  selectRoomComputersLoading
} from "../../app/index";

/**
 * RoomLayout Component
 * 
 * Renders a room's layout as a grid with computers positioned according to layout configuration.
 * Shows loading state or empty state when layout information is not available.
 *
 * @component
 * @param {Object} props - Component props
 * @param {string} props.roomId - ID of the room to fetch layout and computers for
 * @returns {React.ReactElement} The rendered RoomLayout component
 */
const RoomLayout = ({ roomId }) => {
  const dispatch = useAppDispatch();

  // Redux state
  const layout = useAppSelector(selectRoomLayout);
  const computers = useAppSelector(selectRoomComputers);
  const layoutLoading = useAppSelector(selectRoomLayoutLoading);
  const computersLoading = useAppSelector(selectRoomComputersLoading);

  useEffect(() => {
    if (roomId) {
      dispatch(fetchRoomLayout(roomId));
      dispatch(fetchRoomComputers(roomId));
    }
  }, [dispatch, roomId]);

  if (layoutLoading || computersLoading) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!layout || !computers) {
    return (
      <Empty
        description="No layout information available"
        style={{ margin: "50px 0" }}
      />
    );
  }

  const getComputerAtPosition = (row, col) => {
    return computers.find(
      (computer) =>
        computer.position_row === row && computer.position_col === col
    );
  };

  return (
    <div className="room-layout" style={{ width: '100%' }}>
      <div style={{ marginBottom: '16px' }}>
        <p><strong>Room Layout:</strong> {layout.rows} rows × {layout.cols} columns</p>
      </div>
      
      <div className="layout-grid" style={{ width: '100%' }}>
        <Row gutter={[16, 16]}>
          {Array.from({ length: layout.rows }, (_, rowIndex) => (
            <Col key={rowIndex} span={24}>
              <Row gutter={[16, 16]}>
                {Array.from({ length: layout.cols }, (_, colIndex) => {
                  const computer = getComputerAtPosition(rowIndex, colIndex);
                  return (
                    <Col
                      key={`${rowIndex}-${colIndex}`}
                      span={24 / layout.cols}
                      style={{ minHeight: "200px" }}
                    >
                      {computer ? (
                        <ComputerCard computer={computer} />
                      ) : (
                        <Card 
                          hoverable
                          className="empty-cell-card" 
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
                            <p style={{ marginTop: '8px', color: '#999', margin: '4px 0', fontSize: '11px' }}>Position ({colIndex}, {rowIndex})</p>
                          </div>
                        </Card>
                      )}
                    </Col>
                  );
                })}
              </Row>
            </Col>
          ))}
        </Row>
      </div>
    </div>
  );
};

export default RoomLayout;