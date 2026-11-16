import { useRef, useState } from 'react'
import { FormError, PageHeader } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { sendTestEmail } from '../../services/emailService.js'

// Admin page to check that the server's Gmail SMTP settings work.
export function EmailTest() {
  const toast = useToast()
  const inputRef = useRef(null)
  const [toEmail, setToEmail] = useState('')
  const [error, setError] = useState('')
  const [sentTo, setSentTo] = useState('')
  const [isSending, setIsSending] = useState(false)

  const handleSubmit = useSingleFlight(async (event) => {
    event.preventDefault()
    setError('')
    setSentTo('')
    setIsSending(true)

    try {
      const recipient = toEmail.trim()
      await sendTestEmail(recipient)
      setSentTo(recipient)
      toast.success(`Test email sent to ${recipient}.`)
    } catch (requestError) {
      // 404 = the server has the test endpoint switched off (Email:EnableTestEndpoint).
      setError(
        requestError.response?.status === 404
          ? 'The test email feature is turned off on this server.'
          : getApiErrorMessage(requestError, 'The test email could not be sent.'),
      )
      inputRef.current?.focus()
    } finally {
      setIsSending(false)
    }
  })

  return (
    <>
      <PageHeader
        title="Email test"
        description="Send a test message through the server's Gmail account to confirm email delivery works."
      />

      <section className="panel" style={{ maxWidth: 520 }}>
        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <FormError message={error} focusOnShow={false} />
          {sentTo && (
            <p className="muted-note" role="status">
              Sent. Check the inbox of <strong>{sentTo}</strong> (and the spam folder).
            </p>
          )}
          <div className="field">
            <label htmlFor="test-email-to">Recipient email address</label>
            <input
              id="test-email-to"
              ref={inputRef}
              type="email"
              autoComplete="off"
              placeholder="recipient@gmail.com"
              value={toEmail}
              onChange={(event) => setToEmail(event.target.value)}
              required
            />
          </div>
          <button className="primary-button" type="submit" disabled={isSending || !toEmail.trim()}>
            {isSending ? 'Sending...' : 'Send test email'}
          </button>
        </form>
      </section>
    </>
  )
}
