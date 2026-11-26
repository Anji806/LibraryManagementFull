import { useEffect, useRef } from 'react'

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID
const GIS_SRC = 'https://accounts.google.com/gsi/client'

let scriptPromise
function loadGoogleScript() {
  if (window.google?.accounts?.id) return Promise.resolve()
  scriptPromise ??= new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.src = GIS_SRC
    script.async = true
    script.onload = resolve
    script.onerror = () => {
      scriptPromise = undefined
      reject(new Error('Google script failed to load'))
    }
    document.head.appendChild(script)
  })
  return scriptPromise
}

// Renders Google's "Continue with Google" button. Hidden when no client id is configured.
export function GoogleSignInButton({ onCredential, onError, disabled }) {
  const containerRef = useRef(null)
  const callbackRef = useRef(onCredential)
  const isConfigured = Boolean(GOOGLE_CLIENT_ID)
  useEffect(() => {
    callbackRef.current = onCredential
  })

  useEffect(() => {
    if (!isConfigured) return undefined
    let cancelled = false

    loadGoogleScript()
      .then(() => {
        if (cancelled || !containerRef.current) return
        window.google.accounts.id.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: (response) => callbackRef.current?.(response.credential),
        })
        window.google.accounts.id.renderButton(containerRef.current, {
          theme: 'outline',
          size: 'large',
          text: 'continue_with',
          shape: 'rectangular',
          width: Math.min(containerRef.current.offsetWidth || 320, 400),
        })
      })
      .catch(() => onError?.('Google sign-in is unavailable right now.'))

    return () => {
      cancelled = true
    }
  }, [isConfigured, onError])

  return (
    <div className="google-signin" style={{ pointerEvents: disabled ? 'none' : 'auto', opacity: disabled ? 0.6 : 1 }}>
      <div className="google-signin-divider" style={{ textAlign: 'center', margin: '16px 0', color: '#6b7280' }}>or</div>
      {isConfigured ? (
        <div ref={containerRef} style={{ display: 'flex', justifyContent: 'center' }} />
      ) : (
        <>
          <button
            className="secondary-button"
            type="button"
            disabled={disabled}
            onClick={() => onError?.('Google sign-in needs to be configured. Set VITE_GOOGLE_CLIENT_ID in the UI environment and GoogleAuth:ClientId in the API configuration.')}
          >
            Continue with Google
          </button>
          <p className="auth-intro" style={{ textAlign: 'center', marginTop: 8 }}>
            Google sign-in is not configured yet.
          </p>
        </>
      )}
    </div>
  )
}
