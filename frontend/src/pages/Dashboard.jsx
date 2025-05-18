/**
 * @fileoverview Main dashboard component for the Computer Management System
 * 
 * This component serves as the landing page after user authentication,
 * providing quick access to user information and common navigation links.
 * 
 * @module Dashboard
 */
import { useSelector } from 'react-redux';
import { Link } from 'react-router-dom';
import { selectAuthUser } from '../app/slices/authSlice';

/**
 * Dashboard Component
 * 
 * Displays a personalized dashboard with:
 * - User profile information
 * - Quick links to frequently accessed sections
 * - Role-specific navigation options (for admins)
 * 
 * @component
 * @returns {React.ReactElement} The rendered Dashboard component
 */
const Dashboard = () => {
  const user = useSelector(selectAuthUser);

  return (
    <div className="w-full p-6 bg-gray-50">
      <div className="mb-8 pb-4 border-b border-gray-200">
        <h1 className="mb-2 text-gray-800 text-3xl font-bold">Dashboard</h1>
        <p className="text-gray-600">Welcome to the Computer Management System</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {/* User Profile Card */}
        <div className="bg-white rounded-xl shadow-md p-6 transition-all hover:shadow-lg">
          <div className="flex items-center mb-4 pb-2 border-b border-gray-200">
            <div className="p-2 bg-blue-100 rounded-lg mr-3">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
              </svg>
            </div>
            <h2 className="text-gray-800 text-xl font-medium">User Profile</h2>
          </div>
          <div className="profile-info space-y-3">
            <div className="flex items-start">
              <span className="text-gray-500 w-24">Username:</span>
              <span className="font-medium text-gray-800">{user?.username}</span>
            </div>
            <div className="flex items-start">
              <span className="text-gray-500 w-24">Role:</span>
              <span className="font-medium capitalize text-gray-800">
                {user?.role === 'admin' ? (
                  <span className="bg-purple-100 text-purple-800 text-xs px-2 py-1 rounded-full">Admin</span>
                ) : (
                  <span className="bg-blue-100 text-blue-800 text-xs px-2 py-1 rounded-full">{user?.role}</span>
                )}
              </span>
            </div>
          </div>
        </div>

        {/* Quick Links Card */}
        <div className="bg-white rounded-xl shadow-md p-6 transition-all hover:shadow-lg">
          <div className="flex items-center mb-4 pb-2 border-b border-gray-200">
            <div className="p-2 bg-blue-100 rounded-lg mr-3">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
              </svg>
            </div>
            <h2 className="text-gray-800 text-xl font-medium">Quick Links</h2>
          </div>
          <ul className="list-none p-0 space-y-2">
            <li>
              <Link to="/dashboard" className="flex items-center text-blue-600 no-underline p-2 rounded-lg transition-colors hover:bg-blue-50">
                <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                </svg>
                Dashboard
              </Link>
            </li>
            <li>
              <Link to="/rooms" className="flex items-center text-blue-600 no-underline p-2 rounded-lg transition-colors hover:bg-blue-50">
                <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M8 14v3m4-3v3m4-3v3M3 21h18M3 10h18M3 7l9-4 9 4M4 10h16v11H4V10z" />
                </svg>
                View Rooms
              </Link>
            </li>
            {user?.role === 'admin' && (
              <>
                <li>
                  <Link to="/admin" className="flex items-center text-blue-600 no-underline p-2 rounded-lg transition-colors hover:bg-blue-50">
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                    Admin Panel
                  </Link>
                </li>
                <li>
                  <Link to="/admin/users" className="flex items-center text-blue-600 no-underline p-2 rounded-lg transition-colors hover:bg-blue-50">
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                    </svg>
                    User Management
                  </Link>
                </li>
                <li>
                  <Link to="/admin/computers" className="flex items-center text-blue-600 no-underline p-2 rounded-lg transition-colors hover:bg-blue-50">
                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                    Computer Management
                  </Link>
                </li>
              </>
            )}
          </ul>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
