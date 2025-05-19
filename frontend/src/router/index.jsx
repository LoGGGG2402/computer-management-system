/**
 * @fileoverview Main router configuration for the Computer Management System
 * 
 * This file defines all the application routes and their respective access controls:
 * - Public routes: Accessible without authentication (login page)
 * - Protected routes: Require user authentication
 * - Admin routes: Require authentication with admin role
 * 
 * The router uses React Router v6 with the createBrowserRouter API.
 */
import { createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute, AdminRoute, PublicRoute } from './ProtectedRoute';
import MainLayout from '../layouts/MainLayout';

// Import pages
import LoginPage from '../pages/LoginPage';
import Dashboard from '../pages/Dashboard';
import RoomsListPage from '../pages/room/RoomsListPage'; 
import RoomDetailPage from '../pages/room/RoomDetailPage';
import ComputerDetailPage from '../pages/computer/ComputerDetailPage';
// Import ComputersListPage for general users if needed, or remove if only admin access
// import ComputersListPage from '../pages/computer/ComputersListPage'; 

// Import admin pages
import AdminDashboard from '../pages/Admin/AdminDashboard';
// Import User and Computer list pages for admin routes
import UsersListPage from '../pages/Admin/UsersListPage';
import ComputersListPageAdmin from '../pages/computer/ComputersListPage'; // Use alias if needed or ensure correct import path
import AgentVersionManagementPage from '../pages/Admin/AgentVersionManagementPage'; // Import the new page

/**
 * Not Found component displayed when no route matches the current URL
 * @returns {JSX.Element} - 404 error page component
 */
const NotFound = () => <div>404 - Page Not Found</div>;

/**
 * Application router configuration
 * Organizes routes into three main categories:
 * 1. Public routes - No authentication required
 * 2. Protected routes - Authentication required
 * 3. Admin routes - Authentication with admin role required
 */
const router = createBrowserRouter([
  // Public routes (accessible without authentication)
  {
    element: <PublicRoute />,
    children: [
      {
        path: '/login',
        element: <LoginPage />,
      },
    ]
  },
  
  // Protected routes (require authentication)
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <MainLayout />, // Apply main layout with header & footer
        children: [
          {
            path: '/',
            element: <Dashboard />,
          },
          {
            path: '/dashboard',
            element: <Dashboard />,
          },
          // Room routes
          {
            path: '/rooms',
            element: <RoomsListPage />,
          },
          {
            path: '/rooms/:id',
            element: <RoomDetailPage />,
          },
          // Computer routes (detail view accessible to regular users)
          {
            path: '/computers/:id',
            element: <ComputerDetailPage />,
          },
          // Add a general computer list view for regular users if needed
          // {
          //   path: '/computers',
          //   element: <ComputersListPage />, // Or a different component for non-admins
          // },
        ]
      }
    ]
  },
  
  // Admin routes (require admin role)
  {
    element: <AdminRoute />,
    children: [
      {
        element: <MainLayout />, // Apply main layout with header & footer
        children: [
          {
            path: '/admin', // Admin Dashboard (stats only)
            element: <AdminDashboard />,
          },
          {
            path: '/admin/users', // User Management page
            element: <UsersListPage />,
          },
          {
            path: '/admin/computers', // Computer Management page (admin view)
            element: <ComputersListPageAdmin />, // Use the imported ComputersListPage
          },
          {
            path: '/admin/agent-versions', // Agent Version Management page
            element: <AgentVersionManagementPage />,
          },
        ]
      }
    ]
  },
  
  // 404 route
  {
    path: '*',
    element: <NotFound />
  }
]);

export default router;