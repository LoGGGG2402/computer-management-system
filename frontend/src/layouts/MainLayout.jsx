import { Outlet, useNavigate } from 'react-router-dom';
import { notification } from 'antd';
import Header from './Header';
import { useSocket } from '../contexts/SocketContext';
import { useAuth } from '../contexts/AuthContext';
import { useEffect } from 'react';

// Notification handler component
const NotificationHandler = () => {
  const { socket } = useSocket();
  const { user } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!socket || user?.role !== 'admin') {
      console.log('[NotificationHandler] Not listening for MFA - Conditions not met:', {
        socketExists: !!socket,
        userRole: user?.role
      });
      return;
    }

    console.log('[NotificationHandler] Starting to listen for MFA notifications');

    // Listen for new MFA codes
    socket.on('admin:new_agent_mfa', (data) => {
      console.log('[NotificationHandler] Received MFA notification:', data);
      
      // Create room information text if available
      const positionInfoText = data.positionInfo ? 
        `\nPhòng: ${data.positionInfo.room || 'Không xác định'}` +
        `\nVị trí: (${data.positionInfo.posX || 0}, ${data.positionInfo.posY || 0})` +
        `\nRoom ID: ${data.positionInfo.roomId || 'N/A'}` : '';
      
      // Display alert for easy visibility of MFA code
      alert(`MFA Code: ${data.mfaCode}\nAgent ID: ${data.unique_agent_id}${positionInfoText}`);
      
      notification.info({
        message: 'New Agent MFA',
        description: `Agent ID: ${data.unique_agent_id} requires MFA verification with code: ${data.mfaCode}${positionInfoText}`,
        duration: 10,
        onClick: () => {
          navigate('/admin/agents');
        },
      });
    });

    // Listen for new agent registrations
    socket.on('admin:agent_registered', (data) => {
      notification.success({
        message: 'New Agent Registered',
        description: `A new agent (ID: ${data.unique_agent_id}) has been registered. Computer ID: ${data.computerId}`,
        duration: 8,
        onClick: () => {
          navigate('/admin/computers');
        },
      });
    });

    return () => {
      socket.off('admin:new_agent_mfa');
      socket.off('admin:agent_registered');
    };
  }, [socket, user?.role, navigate]);

  return null;
};

const MainLayout = () => {
  return (
    <div className="flex flex-col min-h-screen bg-gray-50">
      <Header />
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 py-8">
        <Outlet />
      </main>
      <footer className="bg-white border-t border-gray-200 py-6 mt-auto shadow-inner">
        <div className="max-w-7xl mx-auto px-4 text-center">
          <div className="flex flex-col items-center justify-center space-y-2">
            <div className="flex items-center justify-center">
              <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center mr-2">
                <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              </div>
              <span className="text-gray-700 font-medium">Computer Management System</span>
            </div>
            <p className="text-gray-500 text-sm">© {new Date().getFullYear()} All rights reserved</p>
          </div>
        </div>
      </footer>
      <NotificationHandler />
    </div>
  );
};

export default MainLayout;