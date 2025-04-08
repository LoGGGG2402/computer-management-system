import { createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute, AdminRoute, PublicRoute } from './ProtectedRoute';
import MainLayout from '../layouts/MainLayout';

// Import pages
import LoginPage from '../pages/LoginPage';
import Dashboard from '../pages/dashboard/Dashboard';
import RoomPage from '../pages/room/RoomPage'; 
import RoomDetailPage from '../pages/room/RoomDetailPage';
import ComputerDetailPage from '../pages/computer/ComputerDetailPage';

// Import admin pages
import AdminDashboard from '../pages/dashboard/AdminDashboard';

// Placeholder components (these will need to be created later)
const NotFound = () => <div>404 - Page Not Found</div>;

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
            element: <RoomPage />,
          },
          {
            path: '/rooms/:id',
            element: <RoomDetailPage />,
          },
          // Computer routes
          {
            path: '/computers/:id',
            element: <ComputerDetailPage />,
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
          // Use the same RoomPage component, but in admin context
          {
            path: '/admin/rooms',
            element: <RoomPage />,
          },
          // Computer detail page for admin
          {
            path: '/admin/computers/:id',
            element: <ComputerDetailPage />,
          },
          // Computer and User management are now handled within AdminDashboard
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