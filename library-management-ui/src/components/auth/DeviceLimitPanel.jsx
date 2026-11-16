import { useState } from 'react'
import { FormError } from '../ui/PageParts.jsx'

function formatWhen(value) {
  if (!value) return '—'
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/.test(value)
  const date = new Date(hasZone ? value : `${value}Z`)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

// Shown when the account is already signed in on two devices. The user picks
// one to log out; the parent completes the pending sign-in.
export function DeviceLimitPanel({ sessions, error, busyId, onSelect, onCancel }) {
  const [confirmingId, setConfirmingId] = useState(null)
  const busy = busyId !== null

  return (
    <div className="device-limit">
      <h2>Maximum Device Limit Reached</h2>
      <p className="auth-intro">
        You are already logged in on two devices. Select one device to log out and continue.
      </p>

      <FormError message={error} focusOnShow={Boolean(error)} />

      <ul className="device-list">
        {sessions.map((session) => (
          <li className="device-card" key={session.id}>
            <div className="device-info">
              <strong>{session.deviceName}</strong>
              <dl>
                {session.browser && <div><dt>Browser</dt><dd>{session.browser}</dd></div>}
                {session.operatingSystem && <div><dt>System</dt><dd>{session.operatingSystem}</dd></div>}
                <div><dt>Signed in</dt><dd>{formatWhen(session.createdAt)}</dd></div>
                <div><dt>Last active</dt><dd>{formatWhen(session.lastActivityAt)}</dd></div>
                {session.ipAddress && <div><dt>IP address</dt><dd>{session.ipAddress}</dd></div>}
              </dl>
            </div>
            {confirmingId === session.id ? (
              <div className="device-actions">
                <button
                  className="primary-button small"
                  type="button"
                  disabled={busy}
                  onClick={() => onSelect(session)}
                >
                  {busyId === session.id ? 'Logging out...' : 'Confirm'}
                </button>
                <button
                  className="secondary-button small"
                  type="button"
                  disabled={busy}
                  onClick={() => setConfirmingId(null)}
                >
                  Cancel
                </button>
              </div>
            ) : (
              <button
                className="secondary-button small"
                type="button"
                disabled={busy}
                onClick={() => setConfirmingId(session.id)}
              >
                Log out this device
              </button>
            )}
          </li>
        ))}
      </ul>

      <button className="link-button" type="button" disabled={busy} onClick={onCancel}>
        Back to sign in
      </button>
    </div>
  )
}
