import { useRef, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth.js'
import { DeviceLimitPanel } from '../../components/auth/DeviceLimitPanel.jsx'
import { FormError } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { getApiErrorMessage } from '../../services/api.js'
import { handleFormKeyDown } from '../../utils/keyboard.js'

export function Login() {
  const { login, completeLogin, isAuthenticated, isLoading, sessionExpired } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const notice = sessionExpired ? 'Your session has expired. Please sign in again.' : ''
  const [isSubmitting, setIsSubmitting] = useState(false)
  const emailRef = useRef(null)
  // Set when the account is already signed in on two devices.
  const [deviceLimit, setDeviceLimit] = useState(null)
  const [limitError, setLimitError] = useState('')
  const [revokingId, setRevokingId] = useState(null)

  const handleSubmit = useSingleFlight(async (event) => {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      await login({ email, password })
      navigate(location.state?.from?.pathname || '/dashboard', { replace: true })
    } catch (requestError) {
      const limit = requestError.response?.data
      if (requestError.response?.status === 409 && limit?.code === 'DEVICE_LIMIT_REACHED') {
        setLimitError('')
        setDeviceLimit(limit)
        setPassword('')
      } else {
        setError(getApiErrorMessage(requestError, 'Sign in failed.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  })

  // Logs out the chosen device, then finishes this pending sign-in.
  const handleSelectDevice = useSingleFlight(async (session) => {
    setLimitError('')
    setRevokingId(session.id)
    try {
      await completeLogin({
        challengeToken: deviceLimit.challengeToken,
        sessionToRevokeId: session.id,
      })
      navigate(location.state?.from?.pathname || '/dashboard', { replace: true })
    } catch (requestError) {
      if (requestError.response?.status === 400) {
        // The challenge expired, was already used, or the device list changed.
        setDeviceLimit(null)
        setError(requestError.response?.data?.message || 'Please sign in again.')
      } else {
        setLimitError(getApiErrorMessage(requestError, 'The device could not be logged out.'))
      }
    } finally {
      setRevokingId(null)
    }
  })

  function handleCancelDeviceLimit() {
    setDeviceLimit(null)
    setLimitError('')
  }

  // Esc dismisses the error first; pressing it again clears the form and returns to the email field.
  function handleKeyDown(event) {
    if (event.key === 'Escape' && !event.defaultPrevented) {
      event.preventDefault()
      if (error) {
        setError('')
      } else {
        setEmail('')
        setPassword('')
        emailRef.current?.focus()
      }
      return
    }
    handleFormKeyDown(event)
  }

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
           <p className="eyebrow">A quieter place to grow</p> 
           <h1>Knowledge belongs to everyone.</h1> 
           <p>Sign in to continue to your library workspace</p> 
        </div>
         <p className="auth-footer">Library Management System</p> 
      </aside>
      <section className="auth-panel">
        <div className="auth-form-wrap">
          {deviceLimit ? (
            <DeviceLimitPanel
              sessions={deviceLimit.sessions ?? []}
              error={limitError}
              busyId={revokingId}
              onSelect={handleSelectDevice}
              onCancel={handleCancelDeviceLimit}
            />
          ) : (
          <>
          <p className="auth-eyebrow">Welcome back</p>
          <h2>Sign in to your account</h2>
          <p className="auth-intro">Use your library account credentials to continue.</p>
          <form className="auth-form" onSubmit={handleSubmit} onKeyDown={handleKeyDown}>
            <FormError message={error || notice} focusOnShow={Boolean(error)} />
            <div className="field">
              <label htmlFor="login-email">Email address</label>
              <input
                id="login-email"
                ref={emailRef}
                type="email"
                autoComplete="email"
                placeholder="you@example.com"
                value={email}
                onChange = {(event) => setEmail(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="login-password">Password</label>
              <input
                id="login-password"
                type="password"
                autoComplete="current-password"
                placeholder="Enter your password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>
            <button className="primary-button" type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
          <p className="auth-switch">
            New to the library? <Link to="/register">Create an account</Link>
          </p>
          </>
          )}
        </div>
      </section>
    </main>
  )
}
