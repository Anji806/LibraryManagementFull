import { Navigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth.js'

export function RoleRoute({ allowedRoles, children }) {
  const { isAuthenticated, isLoading, role } = useAuth()

  if (isLoading) return <div className="loading-page">Restoring your session...</div>
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (!allowedRoles.includes(role)) return <Navigate to="/403" replace />

  return children
}
