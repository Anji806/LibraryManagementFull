import { useCallback, useEffect, useId, useRef } from 'react'
import { createPortal } from 'react-dom'
import { getFocusable } from '../../utils/keyboard.js'

// The app root is made inert while a dialog is open so focus and assistive tech stay inside it.
let openDialogs = 0

function setBackgroundInert(inert) {
  const root = document.getElementById('root')
  if (root) root.inert = inert
}

const DISCARD_MESSAGE = 'You have unsaved changes. Discard them?'

export function Modal({
  title,
  onClose,
  children,
  busy = false,
  dirty = false,
  role = 'dialog',
  describedBy,
}) {
  const dialogRef = useRef(null)
  const titleId = useId()

  const requestClose = useCallback(() => {
    if (busy) return
    if (dirty && !window.confirm(DISCARD_MESSAGE)) return
    onClose()
  }, [busy, dirty, onClose])

  const requestCloseRef = useRef(requestClose)
  useEffect(() => {
    requestCloseRef.current = requestClose
  }, [requestClose])

  // Move focus in on open, trap it while open, and return it to the opener on close.
  useEffect(() => {
    const opener = document.activeElement
    const dialog = dialogRef.current

    openDialogs += 1
    setBackgroundInert(true)

    const preferred = dialog.querySelector('[data-autofocus]')
    const fallback = getFocusable(dialog.querySelector('.modal-body') ?? dialog)[0]
    ;(preferred ?? fallback ?? dialog).focus()

    return () => {
      openDialogs -= 1
      if (openDialogs === 0) setBackgroundInert(false)

      if (opener instanceof HTMLElement && opener.isConnected) {
        opener.focus()
      } else {
        // The opener was removed (for example a deleted row): land on the page content.
        document.getElementById('main-content')?.focus()
      }
    }
  }, [])

  useEffect(() => {
    function handleKey(event) {
      if (event.key === 'Escape' && !event.defaultPrevented) {
        event.preventDefault()
        requestCloseRef.current()
        return
      }

      if (event.key !== 'Tab') return
      const items = getFocusable(dialogRef.current)
      if (items.length === 0) {
        event.preventDefault()
        dialogRef.current.focus()
        return
      }

      const first = items[0]
      const last = items[items.length - 1]
      const active = document.activeElement
      if (event.shiftKey && (active === first || active === dialogRef.current)) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && active === last) {
        event.preventDefault()
        first.focus()
      } else if (!dialogRef.current.contains(active)) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [])

  return createPortal(
    <div className="modal-backdrop" onMouseDown={requestClose}>
      <div
        ref={dialogRef}
        className="modal"
        role={role}
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={describedBy}
        tabIndex={-1}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="modal-header">
          <h2 id={titleId}>{title}</h2>
          <button
            className="modal-close"
            type="button"
            onClick={requestClose}
            disabled={busy}
            aria-label="Close dialog"
          >
            ×
          </button>
        </div>
        <div className="modal-body">
          {children}
        </div>
      </div>
    </div>,
    document.body,
  )
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel = 'Delete',
  cancelLabel = 'Cancel',
  busy = false,
  onConfirm,
  onCancel,
}) {
  const messageId = useId()

  return (
    <Modal title={title} onClose={onCancel} busy={busy} role="alertdialog" describedBy={messageId}>
      <p className="modal-text" id={messageId}>{message}</p>
      <div className="modal-actions">
        {/* Cancel takes initial focus so Enter/Space never confirms a destructive action by accident. */}
        <button
          className="secondary-button"
          type="button"
          onClick={onCancel}
          disabled={busy}
          data-autofocus
        >
          {cancelLabel}
        </button>
        <button className="danger-button" type="button" onClick={onConfirm} disabled={busy}>
          {busy ? 'Working...' : confirmLabel}
        </button>
      </div>
    </Modal>
  )
}
