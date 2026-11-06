import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth.js'

export function ProtectedRoute({ children }) {
  const { isAuthenticated, isLoading, initializationError } = useAuth()
  const location = useLocation()

  if (isLoading) return <div className="loading-page">Restoring your session...</div>
  if (initializationError) {
    return (
      <main className="status-page">
        <section className="status-card">
          <h1>Session unavailable</h1>
          <p>{initializationError}</p>
        </section>
      </main>
    )
  }
  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return children
}
