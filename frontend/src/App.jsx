import { RouterProvider } from 'react-router-dom'
import router from './router'
import { AuthProvider } from './contexts/AuthContext'
import { SocketProvider } from './contexts/SocketContext'
import { CommandResultProvider } from './contexts/CommandResultContext'

function App() {
  return (
    <AuthProvider>
      <SocketProvider>
        <CommandResultProvider>
          <RouterProvider router={router} />
        </CommandResultProvider>
      </SocketProvider>
    </AuthProvider>
  )
}

export default App
