/**
 * @fileoverview Main layout component with notification handling
 * 
 * This component defines the main layout structure for the application,
 * including the header, content area, footer, and system notification handler.
 * 
 * @module MainLayout
 */
import React, { useEffect } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { Toaster, toast } from 'react-hot-toast';
import Header from './Header';
import { useSocket } from '../contexts/SocketContext';
import { useAuth } from '../contexts/AuthContext';

/**
 * NotificationHandler Component
 * 
 * Manages real-time system notifications using WebSocket events
 * for agent MFA verification and registration alerts.
 * 
 * @component
 * @returns {null} - This component doesn't render any UI elements directly
 */
const NotificationHandler = () => {
  const { socket, isSocketReady } = useSocket();
  const { user } = useAuth();
  const navigate = useNavigate();

  /**
   * Handles WebSocket events for system notifications
   * 
   * @effect
   * @dependency {Object} socket - The WebSocket connection instance
   * @dependency {boolean} isSocketReady - Whether the socket is ready for communication
   * @dependency {Object} user - Current user object with role information
   * @dependency {Function} navigate - Router navigation function
   */
  useEffect(() => {
    if (!socket || !isSocketReady || !user || user.role !== 'admin') {
      return;
    }

    /**
     * Handles new MFA code notifications from agents
     * 
     * @function
     * @param {Object} data - Notification data
     * @param {string} data.mfaCode - The MFA verification code
     * @param {Object} data.positionInfo - Information about the agent's location
     */
    const handleNewMfa = async (data) => {
      const positionInfoText = data.positionInfo ? 
        `Location: ${data.positionInfo.roomName || 'Unknown'}\nPosition: Row ${data.positionInfo.posY || '?'}, Column ${data.positionInfo.posX || '?'}` : 
        null;
      
      toast(
        (t) => (
          <div 
            onClick={async () => {
              try {
                // Try using the Clipboard API first
                if (navigator?.clipboard?.writeText) {
                  await navigator.clipboard.writeText(data.mfaCode);
                } else {
                  // Fallback to older execCommand method
                  const textArea = document.createElement('textarea');
                  textArea.value = data.mfaCode;
                  document.body.appendChild(textArea);
                  textArea.select();
                  const successful = document.execCommand('copy');
                  document.body.removeChild(textArea);
                  
                  if (!successful) {
                    throw new Error('Fallback copy method failed');
                  }
                }
                toast.dismiss(t.id);
                toast.success('MFA code copied to clipboard!');
              } catch (err) {
                console.error('Failed to copy to clipboard:', err);
                toast.error('Failed to copy MFA code - please copy it manually');
              }
            }}
            className="cursor-pointer p-1"
          >
            <div className="font-bold text-blue-600">New Agent MFA</div>
            <div>Agent requires MFA verification with code:</div>
            <div className="bg-gray-100 px-2 py-1 rounded my-1 font-mono select-all">{data.mfaCode}</div>
            {positionInfoText && (
              <div className="text-sm text-gray-600 mt-1">
                {positionInfoText.split('\n').map((line, i) => (
                  <div key={i}>{line}</div>
                ))}
              </div>
            )}
            <div className="text-xs text-gray-500 mt-1">Click to copy code</div>
          </div>
        ),
        {
          duration: 10000,
          style: {
            padding: '16px',
            borderRadius: '8px',
            background: '#fff',
            boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
            border: '1px solid #e2e8f0'
          }
        }
      );
    };

    /**
     * Handles agent registration notifications
     * 
     * @function
     * @param {Object} data - Notification data
     * @param {number|string} data.computerId - ID of the registered computer
     */
    const handleAgentRegistered = (data) => {
      console.log('[NotificationHandler] Received agent registration notification:', data);
      const positionInfoText = data.positionInfo ? 
        `Location: ${data.positionInfo.roomName || 'Unknown'}\nPosition: Row ${data.positionInfo.posY || '?'}, Column ${data.positionInfo.posX || '?'}` : 
        null;
      toast.success(
        (t) => (
          <div 
            onClick={() => {
              toast.dismiss(t.id);
              navigate(`/computers/${data.computerId}`);
            }}
            className="cursor-pointer"
          >
            <div className="font-bold">New Agent Registered</div>
            <div>A new agent has been registered.</div>
            <div className="bg-gray-100 px-2 py-1 rounded my-1 font-mono">{data.computerId}</div>
            {positionInfoText && (
              <div className="text-sm text-gray-600 mt-1">
                {positionInfoText.split('\n').map((line, i) => (
                  <div key={i}>{line}</div>
                ))}
              </div>
            )}
            <div className="text-xs text-gray-500 mt-1">Click to view computer details</div>
          </div>
        ),
        {
          duration: 8000,
          style: {
            padding: '16px',
            cursor: 'pointer'
          }
        }
      );
    };

    // Subscribe to WebSocket events
    socket.on('admin:new_agent_mfa', handleNewMfa);
    socket.on('admin:agent_registered', handleAgentRegistered);

    // Cleanup event listeners on unmount
    return () => {
      if (socket) {
        console.log('[NotificationHandler] Cleaning up listeners');
        socket.off('admin:new_agent_mfa', handleNewMfa);
        socket.off('admin:agent_registered', handleAgentRegistered);
      }
    };
  }, [socket, isSocketReady, user?.role, navigate]);

  return null;
};

/**
 * MainLayout Component
 * 
 * Provides the overall layout structure for the application with:
 * - Header for navigation
 * - Main content area that renders child routes
 * - Footer with copyright information
 * - Notification system for real-time alerts
 * 
 * @component
 * @returns {React.ReactElement} The rendered MainLayout component
 */
const MainLayout = () => {
  return (
    <div className="flex flex-col min-h-screen bg-gray-50 overflow-x-hidden">
      <div className="w-full">
        <Header />
      </div>
      <main className="flex-1 w-full mx-auto px-4 py-8">
        <div className="max-w-7xl mx-auto">
          <Outlet />
        </div>
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
      <Toaster position="top-right" />
    </div>
  );
};

export default MainLayout;