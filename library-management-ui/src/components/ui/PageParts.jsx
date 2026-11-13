import { useEffect, useRef } from 'react'

// Inline form error. Takes focus when it appears (unless `focusOnShow` is false) so
// keyboard and screen-reader users land on the problem; Tab then continues into the form.
export function FormError({ message, focusOnShow = true, style }) {
  const ref = useRef(null)

  useEffect(() => {
    if (message && focusOnShow) ref.current?.focus()
  }, [message, focusOnShow])

  if (!message) return null
  return (
    <p className="form-error" role="alert" tabIndex={-1} ref={ref} style={style}>
      {message}
    </p>
  )
}

export function PageHeader({ title, description, actions }) {
  return (
    <div className="page-heading page-heading-row">
      <div>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </div>
  )
}

export function LoadingState({ label = 'Loading...' }) {
  return (
    <div className="state-box" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  )
}

export function EmptyState({ title, message, action }) {
  return (
    <div className="state-box">
      <h3>{title}</h3>
      {message && <p>{message}</p>}
      {action}
    </div>
  )
}

export function ErrorState({ message, onRetry }) {
  return (
    <div className="state-box state-error" role="alert">
      <h3>Something went wrong</h3>
      <p>{message}</p>
      {onRetry && (
        <button className="secondary-button" type="button" onClick={onRetry} style={{ marginTop: '8px' }}>
          Try again
        </button>
      )}
    </div>
  )
}

// Renders loading / error / empty states, otherwise the children.
export function ResourceView({ resource, isEmpty, empty, children }) {
  if (resource.loading) return <LoadingState />
  if (resource.error) return <ErrorState message={resource.error} onRetry={resource.reload} />
  if (isEmpty) return empty
  return children
}

const STATUS_TONES = {
  Returned: 'neutral',
  Overdue: 'danger',
  'Due Soon': 'warning',
  'Fine Pending': 'warning',
}

export function StatusBadge({ status }) {
  const tone = STATUS_TONES[status] ?? 'active'
  return <span className={`badge badge-${tone}`}>{status}</span>
}
