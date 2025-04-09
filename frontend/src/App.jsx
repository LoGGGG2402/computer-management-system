import { RouterProvider } from 'react-router-dom'
import router from './router'
import { AuthProvider } from './contexts/AuthContext'
import { SocketProvider } from './contexts/SocketContext'
import { CommandHandleProvider } from './contexts/CommandHandleContext'

function App() {
  return (
    <AuthProvider>
      <SocketProvider>
        <CommandHandleProvider>
          <RouterProvider router={router} />
        </CommandHandleProvider>
      </SocketProvider>
    </AuthProvider>
  )
}

export default App
