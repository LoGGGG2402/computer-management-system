import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Loading } from '../components/common';

/**
 * Protected Route component that requires authentication
 * Redirects to login if user is not authenticated
 */
const ProtectedRoute = () => {
  const { isAuthenticated, loading } = useAuth();
  
  // Show loading state while checking authentication
  if (loading) {
    return <Loading />;
  }
  
  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  // Render child routes if authenticated
  return <Outlet />;
};

/**
 * Admin Route component that requires admin role
 * Redirects to dashboard if user is not an admin
 */
const AdminRoute = () => {
  const { isAuthenticated, isAdmin, loading } = useAuth();
  
  // Show loading state while checking authentication
  if (loading) {
    return <Loading />;
  }
  
  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  // Redirect to dashboard if authenticated but not admin
  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />;
  }
  
  // Render child routes if authenticated and admin
  return <Outlet />;
};

/**
 * Public Route component that redirects to dashboard if already authenticated
 * Used for login and register pages
 */
const PublicRoute = () => {
  const { isAuthenticated, loading } = useAuth();
  
  // Show loading state while checking authentication
  if (loading) {
    return <Loading />;
  }
  
  // Redirect to dashboard if already authenticated
  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }
  
  // Render child routes if not authenticated
  return <Outlet />;
};

export { ProtectedRoute, AdminRoute, PublicRoute };