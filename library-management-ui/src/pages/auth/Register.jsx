import { useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth.js'
import { FormError } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { getApiErrorMessage } from '../../services/api.js'

export function Register() {
  const { register, isAuthenticated, isLoading } = useAuth()
  const navigate = useNavigate()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = useSingleFlight(async (event) => {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      await register({ fullName, email, password })
      navigate('/dashboard', { replace: true })
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Account creation failed.'))
    } finally {
      setIsSubmitting(false)
    }
  })

  if (isLoading) return <div className="loading-page">Restoring your session...</div>
  if (isAuthenticated) return <Navigate to="/dashboard" replace />

  return (
    <main className="auth-shell">
      <aside className="auth-aside">
        <div className="brand">
          <span>Greenleaf Library</span>
        </div>
        <div className="auth-aside-copy">
          <img className="auth-hero-logo" src="/Library_icon.png" alt="Greenleaf Library" width="220" height="220" />
          <p className="eyebrow">Make room for a new story</p>
          <h1>Your next chapter starts here.</h1>
          <p>Create a library account to explore the catalog and track your borrowing activity.</p>
        </div>
        <p className="auth-footer">New registrations receive the Member role.</p>
      </aside>
      <section className="auth-panel">
        <div className="auth-form-wrap">
          <p className="auth-eyebrow">Join the library</p>
          <h2>Create your account</h2>
          <p className="auth-intro">Enter your details to register as a library member.</p>
          <form className="auth-form" onSubmit={handleSubmit}>
            <FormError message={error} />
            <div className="field">
              <label htmlFor="register-name">Full name</label>
              <input
                id="register-name"
                type="text"
                autoComplete="name"
                placeholder="Your full name"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="register-email">Email address</label>
              <input
                id="register-email"
                type="email"
                autoComplete="email"
                placeholder="you@example.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="register-password">Password</label>
              <input
                id="register-password"
                type="password"
                autoComplete="new-password"
                minLength={6}
                placeholder="At least 6 characters"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>
            <button className="primary-button" type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Creating account...' : 'Create account'}
            </button>
          </form>
          <p className="auth-switch">
            Already registered? <Link to="/login">Sign in</Link>
          </p>
        </div>
      </section>
    </main>
  )
}
