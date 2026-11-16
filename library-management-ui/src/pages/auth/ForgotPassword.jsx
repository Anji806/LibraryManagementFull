import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AuthShell } from '../../components/auth/AuthShell.jsx'
import { FormError } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { getApiErrorMessage } from '../../services/api.js'
import { forgotPassword } from '../../services/authService.js'

export function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [error, setError] = useState('')
  const [submittedTo, setSubmittedTo] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = useSingleFlight(async (event) => {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      const address = email.trim()
      await forgotPassword(address)
      setSubmittedTo(address)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'The request could not be sent. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  })

  if (submittedTo) {
    // The same message is shown whether or not the address has an account.
    return (
      <AuthShell eyebrow="Check your inbox" title="Reset link requested">
        <p className="auth-intro" role="status">
          If an account exists for <strong>{submittedTo}</strong>, we have sent a password reset link.
          It expires in 15 minutes and can be used once.
        </p>
        <p className="auth-intro">
          Nothing arrived? Check your spam folder, or wait a minute and{' '}
          <button className="link-button" type="button" onClick={() => setSubmittedTo('')}>
            try again
          </button>.
        </p>
        <p className="auth-switch">
          <Link to="/login">Back to sign in</Link>
        </p>
      </AuthShell>
    )
  }

  return (
    <AuthShell
      eyebrow="Account recovery"
      title="Forgot your password?"
      intro="Enter the email address of your library account and we will send you a link to choose a new password."
    >
      <form className="auth-form" onSubmit={handleSubmit}>
        <FormError message={error} />
        <div className="field">
          <label htmlFor="forgot-email">Email address</label>
          <input
            id="forgot-email"
            type="email"
            autoComplete="email"
            placeholder="you@example.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoFocus
          />
        </div>
        <button className="primary-button" type="submit" disabled={isSubmitting || !email.trim()}>
          {isSubmitting ? 'Sending...' : 'Send reset link'}
        </button>
      </form>
      <p className="auth-switch">
        Remembered it? <Link to="/login">Back to sign in</Link>
      </p>
    </AuthShell>
  )
}
