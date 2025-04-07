import { createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute, AdminRoute, PublicRoute } from './ProtectedRoute';
import MainLayout from '../layouts/MainLayout';

// Import pages
import LoginPage from '../pages/LoginPage';
import Dashboard from '../pages/dashboard/Dashboard';

// Placeholder components (these will need to be created later)
const NotFound = () => <div>404 - Page Not Found</div>;
const AdminDashboard = () => <div>Admin Dashboard</div>;
const UserManagement = () => <div>User Management</div>;

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
            path: '/admin',
            element: <AdminDashboard />,
          },
          {
            path: '/admin/users',
            element: <UserManagement />,
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