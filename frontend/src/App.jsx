import { RouterProvider } from 'react-router-dom'
import router from './router'
import { useEffect } from 'react'
import { useAppDispatch, initializeAuth } from './app/index'

function App() {
  const dispatch = useAppDispatch();

  useEffect(() => {
    // Khởi tạo auth state từ localStorage (nếu có)
    dispatch(initializeAuth());
  }, [dispatch]);

  return <RouterProvider router={router} />
}

export default App
