import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ToastContext } from './toastContext.js'

function Toast({ toast, onDismiss }) {
  const [paused, setPaused] = useState(false)

  // Auto-dismiss, but never while the pointer or keyboard focus is on the toast.
  useEffect(() => {
    if (paused) return undefined
    const timer = window.setTimeout(
      () => onDismiss(toast.id),
      toast.type === 'error' ? 6000 : 3500,
    )
    return () => window.clearTimeout(timer)
  }, [paused, toast.id, toast.type, onDismiss])

  return (
    <div
      className={`toast toast-${toast.type}`}
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={() => setPaused(false)}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          event.stopPropagation()
          onDismiss(toast.id)
        }
      }}
    >
      <span>{toast.message}</span>
      <button type="button" aria-label="Dismiss notification" onClick={() => onDismiss(toast.id)}>
        ×
      </button>
    </div>
  )
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])
  const nextId = useRef(0)

  const dismiss = useCallback((id) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const push = useCallback((type, message) => {
    const id = nextId.current++
    setToasts((current) => [...current, { id, type, message }])
  }, [])

  const value = useMemo(
    () => ({
      success: (message) => push('success', message),
      error: (message) => push('error', message),
    }),
    [push],
  )

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-region" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <Toast key={toast.id} toast={toast} onDismiss={dismiss} />
        ))}
      </div>
    </ToastContext.Provider>
  )
}
