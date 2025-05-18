import { Navigate, Outlet } from 'react-router-dom';
import { LoadingComponent } from '../components/common';
import { useAppSelector, selectIsAuthenticated, selectUserRole } from '../app/index';

/**
 * Protected Route component that requires authentication
 * Redirects to login if user is not authenticated
 */
const ProtectedRoute = () => {
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const loading = useAppSelector(state => state.auth.loading);
  
  // Show loading state while checking authentication
  if (loading) {
    return <LoadingComponent />;
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
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const userRole = useAppSelector(selectUserRole);
  const loading = useAppSelector(state => state.auth.loading);
  
  // Show loading state while checking authentication
  if (loading) {
    return <LoadingComponent />;
  }
  
  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  // Redirect to dashboard if authenticated but not admin
  if (userRole !== 'admin') {
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
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const loading = useAppSelector(state => state.auth.loading);
  
  // Show loading state while checking authentication
  if (loading) {
    return <LoadingComponent />;
  }
  
  // Redirect to dashboard if already authenticated
  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }
  
  // Render child routes if not authenticated
  return <Outlet />;
};

export { ProtectedRoute, AdminRoute, PublicRoute };