import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AuthShell } from '../../components/auth/AuthShell.jsx'
import { FormError } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { getApiErrorMessage } from '../../services/api.js'
import { resetPassword } from '../../services/authService.js'

const MIN_LENGTH = 6 // same rule as registration

// Opened from the link in the reset email: /reset-password?token=...&email=...
export function ResetPassword() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const email = searchParams.get('email') ?? ''

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [isDone, setIsDone] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = useSingleFlight(async (event) => {
    event.preventDefault()
    setError('')

    if (newPassword.length < MIN_LENGTH) {
      setError(`Your password must be at least ${MIN_LENGTH} characters long.`)
      return
    }
    if (newPassword !== confirmPassword) {
      setError('The passwords do not match.')
      return
    }

    setIsSubmitting(true)
    try {
      await resetPassword({ email, token, newPassword, confirmPassword })
      setIsDone(true)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'The password could not be reset.'))
    } finally {
      setIsSubmitting(false)
    }
  })

  if (isDone) {
    return (
      <AuthShell eyebrow="All set" title="Password updated">
        <p className="auth-intro" role="status">
          Your password has been changed. For your security you have been signed out everywhere.
        </p>
        <Link className="primary-button" to="/login" style={{ display: 'block', textAlign: 'center' }}>
          Sign in
        </Link>
      </AuthShell>
    )
  }

  if (!token || !email) {
    return (
      <AuthShell eyebrow="Account recovery" title="This link is not valid">
        <p className="auth-intro">
          The reset link is incomplete. Open the link from your email again, or request a new one.
        </p>
        <p className="auth-switch">
          <Link to="/forgot-password">Request a new link</Link>
        </p>
      </AuthShell>
    )
  }

  return (
    <AuthShell
      eyebrow="Account recovery"
      title="Choose a new password"
      intro={`Setting a new password for ${email}.`}
    >
      <form className="auth-form" onSubmit={handleSubmit}>
        <FormError message={error} />
        <div className="field">
          <label htmlFor="reset-password">New password</label>
          <input
            id="reset-password"
            type="password"
            autoComplete="new-password"
            minLength={MIN_LENGTH}
            placeholder={`At least ${MIN_LENGTH} characters`}
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            required
            autoFocus
          />
        </div>
        <div className="field">
          <label htmlFor="reset-confirm">Confirm new password</label>
          <input
            id="reset-confirm"
            type="password"
            autoComplete="new-password"
            placeholder="Type it again"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            required
          />
        </div>
        <button className="primary-button" type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Saving...' : 'Reset password'}
        </button>
      </form>
      <p className="auth-switch">
        Link expired? <Link to="/forgot-password">Request a new one</Link>
      </p>
    </AuthShell>
  )
}
