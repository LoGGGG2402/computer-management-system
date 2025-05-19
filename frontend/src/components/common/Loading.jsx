import React from 'react';
import { Spin, Typography } from 'antd';

const { Text } = Typography;

/**
 * Reusable loading component that can be used in different contexts:
 * - Full page loading
 * - Section loading
 * - Inline loading with customizable size
 *
 * @param {Object} props - Component props
 * @param {string} [props.tip='Đang tải...'] - Text to show below the spinner
 * @param {string} [props.size='default'] - Size of the spinner ('small', 'default', or 'large')
 * @param {string} [props.type='section'] - Type of loading view ('inline', 'section', or 'page')
 * @param {Object} [props.style] - Additional custom styles to apply
 * @param {string} [props.className] - Additional CSS classes to apply
 */
const Loading = ({
  tip = 'Đang tải...',
  size = 'default',
  type = 'section',
  style = {},
  className = '',
}) => {
  // Style based on the loading type
  let containerStyle = {};

  switch (type) {
    case 'page':
      containerStyle = {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        flexDirection: 'column',
        minHeight: '100vh',
        ...style,
      };
      break;
    case 'section':
      containerStyle = {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        flexDirection: 'column',
        padding: '40px 0',
        ...style,
      };
      break;
    case 'inline':
      containerStyle = {
        display: 'inline-flex',
        alignItems: 'center',
        ...style,
      };
      break;
    default:
      containerStyle = {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        ...style,
      };
  }

  return (
    <div className={`loading-component ${className}`} style={containerStyle}>
      <Spin size={size} />
      {tip && type !== 'inline' && (
        <Text
          type="secondary"
          style={{ marginTop: '12px', fontSize: type === 'page' ? '16px' : '14px' }}
        >
          {tip}
        </Text>
      )}
    </div>
  );
};

export default Loading;