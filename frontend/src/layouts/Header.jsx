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
  UserOutlined,
  LogoutOutlined,
  AppstoreOutlined,
  TeamOutlined,
  LaptopOutlined,
  ApiOutlined,
} from "@ant-design/icons";

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
  const [userMenuOpen, setUserMenuOpen] = useState(false);

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
                <AppstoreOutlined className="text-lg" />
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
                    <DashboardOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Admin Panel</span>
                  </Link>
                  <Link
                    to="/admin/users"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <TeamOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Users</span>
                  </Link>
                  <Link
                    to="/admin/computers"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <LaptopOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">Computers</span>
                  </Link>
                  <Link
                    to="/admin/agent-versions"
                    className="text-gray-700 hover:text-blue-600 hover:bg-blue-50 px-2 xl:px-3 py-2 rounded-md text-sm font-medium transition-colors flex items-center whitespace-nowrap"
                  >
                    <ApiOutlined className="text-lg" />
                    <span className="hidden xl:inline ml-2">
                      Agent Versions
                    </span>
                  </Link>
                </>
              )}
            </div>
          </nav>

          {/* User Profile and Logout */}
          <div className="hidden sm:flex items-center justify-end bg-transparent">
            <div className="relative bg-transparent group">
              <div
                className="flex items-center cursor-pointer h-16 px-4 rounded-lg transition-all duration-200 ease-in-out"
              >
                <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center mr-3 ring-1 ring-blue-200 group-hover:ring-blue-300 transition-all duration-200">
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
                      d="M12 2C9.24 2 7 4.24 7 7C7 9.76 9.24 12 12 12C14.76 12 17 9.76 17 7C17 4.24 14.76 2 12 2ZM12 14C8.67 14 2 15.34 2 18.67V20H22V18.67C22 15.34 15.33 14 12 14Z"
                    />
                  </svg>
                </div>
                <div className="flex flex-col justify-center h-10">
                  <span className="text-base font-semibold text-gray-800 max-w-[180px] tracking-tight leading-none group-hover:text-blue-600 transition-colors duration-200 whitespace-nowrap overflow-hidden text-ellipsis">
                    {user?.username || "User"}
                  </span>
                  <span className="text-[9px] text-gray-500 capitalize mt-0.5">
                    {user?.role === "admin" ? (
                      <span className="bg-gradient-to-r from-purple-100 to-purple-50 text-purple-800 px-1.5 py-[1px] rounded-full font-medium tracking-wide shadow-sm hover:shadow-md transition-all duration-200 flex items-center">
                        Admin
                      </span>
                    ) : (
                      <span className="bg-gradient-to-r from-blue-100 to-blue-50 text-blue-800 px-1.5 py-[1px] rounded-full font-medium tracking-wide shadow-sm hover:shadow-md transition-all duration-200 flex items-center">
                        {user?.role || "User"}
                      </span>
                    )}
                  </span>
                </div>
              </div>
              <div className="absolute right-0 mt-2 w-60 rounded-xl shadow-lg bg-white/90 backdrop-blur-md ring-1 ring-gray-900/10 z-50 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-300 ease-in-out transform origin-top-right scale-95 group-hover:scale-100">
                <div className="p-2">
                  <button
                    onClick={handleLogout}
                    className="w-full flex items-center px-4 py-3 text-sm text-gray-700 hover:bg-red-500 hover:text-white rounded-lg transition-all duration-200 ease-in-out group/logout"
                  >
                    <LogoutOutlined className="mr-3 text-lg transition-transform duration-200 group-hover/logout:scale-110 group-hover/logout:text-white" />{" "}
                    <span className="font-medium">Logout</span>
                  </button>
                </div>
              </div>
            </div>
          </div>

          {/* Mobile Menu Button */}
          <div className="sm:hidden flex items-center justify-center h-16">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="relative w-10 h-10 flex items-center justify-center rounded-lg bg-blue-100 hover:bg-blue-200 transition-all duration-300 ease-in-out group"
              aria-label={mobileMenuOpen ? "Close menu" : "Open menu"}
            >
              <div className="absolute inset-0 flex items-center justify-center">
                <span
                  className={`absolute block h-0.5 w-6 bg-blue-600 transform transition-all duration-300 ease-in-out ${
                    mobileMenuOpen ? "rotate-45" : "-translate-y-2"
                  }`}
                />
                <span
                  className={`absolute block h-0.5 w-6 bg-blue-600 transition-all duration-300 ease-in-out ${
                    mobileMenuOpen ? "opacity-0" : "opacity-100"
                  }`}
                />
                <span
                  className={`absolute block h-0.5 w-6 bg-blue-600 transform transition-all duration-300 ease-in-out ${
                    mobileMenuOpen ? "-rotate-45" : "translate-y-2"
                  }`}
                />
              </div>
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Menu */}
      {mobileMenuOpen && (
        <div className="sm:hidden animate-slideDown">
          <div className="px-2 pt-2 pb-3 space-y-1 sm:px-3 bg-white shadow-lg rounded-b-lg">
            <Link
              to="/dashboard"
              className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
              onClick={() => setMobileMenuOpen(false)}
            >
              <AppstoreOutlined className="mr-3 text-lg" />
              Dashboard
            </Link>

            <Link
              to="/rooms"
              className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
              onClick={() => setMobileMenuOpen(false)}
            >
              <HomeOutlined className="mr-3 text-lg" />
              Rooms
            </Link>

            {/* Admin Links */}
            {isAdmin && (
              <>
                <Link
                  to="/admin"
                  className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <DashboardOutlined className="mr-3 text-lg" />
                  Admin Panel
                </Link>
                <Link
                  to="/admin/users"
                  className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <TeamOutlined className="mr-3 text-lg" />
                  User Management
                </Link>
                <Link
                  to="/admin/computers"
                  className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <LaptopOutlined className="mr-3 text-lg" />
                  Computer Management
                </Link>
                <Link
                  to="/admin/agent-versions"
                  className="block px-4 py-3 rounded-lg text-base font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 flex items-center transition-all duration-200 ease-in-out"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  <ApiOutlined className="mr-3 text-lg" />
                  Agent Versions
                </Link>
              </>
            )}

            {/* User Profile and Logout */}
            <div className="pt-4 pb-3 border-t border-gray-200 mt-2">
              <div className="flex items-center px-4 py-2">
                <div className="flex-shrink-0">
                  <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center ring-2 ring-blue-200">
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
                        d="M12 2C9.24 2 7 4.24 7 7C7 9.76 9.24 12 12 12C14.76 12 17 9.76 17 7C17 4.24 14.76 2 12 2ZM12 14C8.67 14 2 15.34 2 18.67V20H22V18.67C22 15.34 15.33 14 12 14Z"
                      />
                    </svg>
                  </div>
                </div>
                <div className="ml-4">
                  <div className="text-base font-semibold text-gray-800 truncate max-w-[200px] tracking-tight">
                    {user?.username}
                  </div>
                  <div className="text-sm font-medium text-gray-500 capitalize mt-1">
                    {user?.role === "admin" ? (
                      <span className="bg-purple-100 text-purple-800 text-xs px-2.5 py-1 rounded-full font-medium tracking-wide">
                        Admin
                      </span>
                    ) : (
                      <span className="bg-blue-100 text-blue-800 text-xs px-2.5 py-1 rounded-full font-medium tracking-wide">
                        {user?.role}
                      </span>
                    )}
                  </div>
                </div>
              </div>
              <div className="mt-4 px-4">
                <button
                  onClick={() => {
                    handleLogout();
                    setMobileMenuOpen(false);
                  }}
                  className="w-full flex items-center justify-center px-4 py-3 border border-transparent rounded-lg shadow-sm text-base font-medium text-white bg-red-600 hover:bg-red-700 transition-all duration-200 ease-in-out transform hover:scale-[1.02]"
                >
                  <LogoutOutlined className="mr-2 text-lg" />
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
