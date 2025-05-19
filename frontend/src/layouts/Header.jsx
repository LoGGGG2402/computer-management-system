/**
 * @fileoverview Main navigation header component
 * 
 * This component handles the top navigation bar of the application,
 * providing links to main sections and user authentication controls.
 * It has responsive design with mobile menu support.
 * 
 * @module Header
 */
import { useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { Link, useNavigate } from "react-router-dom";
import { 
  DashboardOutlined, 
  HomeOutlined, 
  SettingOutlined, 
  UserOutlined, 
  DesktopOutlined, 
  CodeOutlined,
  LogoutOutlined,
  MenuOutlined,
  CloseOutlined
} from '@ant-design/icons';

/**
 * Header Component
 * 
 * Provides the main navigation header with:
 * - Application logo and brand
 * - Navigation links based on user role
 * - User profile information display
 * - Authentication controls (logout button)
 * - Responsive mobile menu
 * 
 * @component
 * @returns {React.ReactElement|null} The rendered Header component or null if user is not authenticated
 */
const Header = () => {
  const { user, isAuthenticated, isAdmin, logoutAction } = useAuth();
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  /**
   * Handles user logout action
   * 
   * @function
   */
  const handleLogout = () => {
    logoutAction();
    navigate("/login");
  };

  if (!isAuthenticated) return null;

  return (
    <header className="bg-white shadow-md sticky top-0 z-50 w-full">
      <div className="w-full px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          {/* Logo and Brand Section */}
          <div className="flex-shrink-0 flex items-center">
            <Link to="/dashboard" className="flex items-center no-underline">
              <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center mr-3">
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  className="h-6 w-6 text-blue-600"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                  />
                </svg>
              </div>
              <span className="text-xl font-bold text-gray-800">CMS</span>
            </Link>
          </div>

          {/* Desktop Navigation Links */}
          <nav className="hidden sm:flex items-center justify-center w-2/4">
            <div className="flex items-center justify-between w-full space-x-1 xl:space-x-4">
              <Link
                to="/dashboard"
                className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
              >
                <DashboardOutlined className="text-lg" />
                <span className="hidden xl:inline ml-2">Dashboard</span>
              </Link>

              <Link
                to="/rooms"
                className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
              >
                <HomeOutlined className="text-lg" />
                <span className="hidden xl:inline ml-2">Rooms</span>
              </Link>
              {isAdmin && (
                <>
                  <Link
                    to="/admin"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <SettingOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Admin Panel</span>
                  </Link>
                  <Link
                    to="/admin/users"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <UserOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Users</span>
                  </Link>
                  <Link
                    to="/admin/computers"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <DesktopOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Computers</span>
                  </Link>
                  <Link
                    to="/admin/agent-versions"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <CodeOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Agent Versions</span>
                  </Link>
                </>
              )}
            </div>
          </nav>

          {/* User Profile and Logout */}
          <div className="hidden sm:flex items-center justify-end w-1/4">
            <div className="flex items-center space-x-2 xl:space-x-3">
              <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center">
                <UserOutlined className="h-4 w-4 text-blue-600" />
              </div>
              <div className="flex flex-col items-start">
                <span className="text-sm font-medium text-gray-700 truncate max-w-[100px] xl:max-w-[150px]">
                  {user?.username}
                </span>
                <span className="text-xs text-gray-500 capitalize">
                  {user?.role === "admin" ? (
                    <span className="bg-purple-100 text-purple-800 text-xs px-1.5 py-0.5 rounded-full">
                      Admin
                    </span>
                  ) : (
                    <span>{user?.role}</span>
                  )}
                </span>
              </div>
            </div>

            <button
              onClick={handleLogout}
              className="ml-2 inline-flex items-center px-2 xl:px-3 py-2 border border-transparent text-sm leading-4 font-medium rounded-md text-white bg-red-600 shadow-sm hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 transition-colors whitespace-nowrap"
            >
              <LogoutOutlined className="text-lg" />
              <span className="hidden xl:inline ml-2">Logout</span>
            </button>
          </div>

          {/* Mobile Menu Button */}
          <div className="sm:hidden flex items-center">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="inline-flex items-center justify-center p-2 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-50 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-blue-500"
            >
              <span className="sr-only">Open main menu</span>
              {mobileMenuOpen ? <CloseOutlined className="h-6 w-6" /> : <MenuOutlined className="h-6 w-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Menu */}
      {mobileMenuOpen && (
        <div className="sm:hidden">
          <div className="px-2 pt-2 pb-3 space-y-1 sm:px-3 bg-white shadow-lg">
            <Link
              to="/dashboard"
              className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
              onClick={() => setMobileMenuOpen(false)}
            >
              <DashboardOutlined className="mr-2" />
              Dashboard
            </Link>

            <Link
              to="/rooms"
              className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
              onClick={() => setMobileMenuOpen(false)}
            >
              <HomeOutlined className="mr-2" />
              Rooms
            </Link>

            {/* Admin Links */}
            {isAdmin && (
              <>
                <Link
                  to="/admin"
                  className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <SettingOutlined className="mr-2" />
                  Admin Panel
                </Link>
                <Link
                  to="/admin/users"
                  className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <UserOutlined className="mr-2" />
                  User Management
                </Link>
                <Link
                  to="/admin/computers"
                  className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <DesktopOutlined className="mr-2" />
                  Computer Management
                </Link>
                <Link
                  to="/admin/agent-versions"
                  className="block px-3 py-2 rounded-md text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <CodeOutlined className="mr-2" />
                  Agent Versions
                </Link>
              </>
            )}

            {/* User Profile and Logout */}
            <div className="pt-4 pb-3 border-t border-gray-200">
              <div className="flex items-center px-3">
                <div className="flex-shrink-0">
                  <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center">
                    <UserOutlined className="h-5 w-5 text-blue-600" />
                  </div>
                </div>
                <div className="ml-3">
                  <div className="text-base font-medium text-gray-800 truncate max-w-[200px]">
                    {user?.username}
                  </div>
                  <div className="text-sm font-medium text-gray-500 capitalize">
                    {user?.role}
                  </div>
                </div>
              </div>
              <div className="mt-3 px-2">
                <button
                  onClick={() => {
                    handleLogout();
                    setMobileMenuOpen(false);
                  }}
                  className="w-full flex items-center justify-center px-4 py-2 border border-transparent rounded-md shadow-sm text-base font-medium text-white bg-red-600 hover:bg-red-700"
                >
                  <LogoutOutlined className="mr-2" />
                  Logout
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </header>
  );
};

export default Header;
