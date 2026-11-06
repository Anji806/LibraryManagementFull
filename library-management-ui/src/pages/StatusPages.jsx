import { Link } from 'react-router-dom'

export function Forbidden() {
  return (
    <main className="status-page">
      <section className="status-card">
        <p className="status-code">403</p>
        <h1>Access not permitted</h1>
        <p>
          You are signed in, but your account does not have permission to open this area.
          Your session is still active.
        </p>
        <div className="status-actions">
          <Link className="primary-button" to="/dashboard">Back to dashboard</Link>
        </div>
      </section>
    </main>
  )
}

export function NotFound() {
  return (
    <main className="status-page">
      <section className="status-card">
        <p className="status-code">404</p>
        <h1>Page not found</h1>
        <p>The page you are looking for does not exist or may have moved.</p>
        <div className="status-actions">
          <Link className="primary-button" to="/dashboard">Go to dashboard</Link>
        </div>
      </section>
    </main>
  )
}
