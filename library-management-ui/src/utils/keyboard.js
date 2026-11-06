export const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

export function getFocusable(container) {
  return Array.from(container.querySelectorAll(FOCUSABLE_SELECTOR)).filter(
    (element) => !element.closest('[inert]') && element.getClientRects().length > 0,
  )
}

// Up/Down moves between table rows, keeping the same column position when possible.
// Only acts on links and buttons so text fields and selects keep native arrow behaviour.
export function handleTableKeyDown(event) {
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return
  if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return

  const target = event.target
  if (!(target instanceof HTMLElement) || !target.matches('a[href], button')) return

  const row = target.closest('tr')
  if (!row) return

  const rows = Array.from(event.currentTarget.querySelectorAll('tbody tr'))
  const nextRow = rows[rows.indexOf(row) + (event.key === 'ArrowDown' ? 1 : -1)]
  if (!nextRow) return

  const currentItems = getFocusable(row)
  const nextItems = getFocusable(nextRow)
  if (nextItems.length === 0) return

  const column = Math.max(0, currentItems.indexOf(target))
  event.preventDefault()
  nextItems[Math.min(column, nextItems.length - 1)].focus()
}

const isTextField = (element) =>
  element instanceof HTMLElement && element.matches('input:not([type="checkbox"]):not([type="radio"]):not([type="button"]):not([type="submit"]), textarea, select')

// Arrow keys / Home / End move between the links or buttons of a menu or toolbar.
// Up and Left go to the previous item, Down and Right to the next. Text fields are left alone
// so the caret keeps moving with Left/Right.
export function handleMenuKeyDown(event) {
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return
  if (!['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
  if (isTextField(event.target)) return

  const items = getFocusable(event.currentTarget)
  const index = items.indexOf(document.activeElement)
  if (index === -1) return

  let next
  if (event.key === 'ArrowDown' || event.key === 'ArrowRight') next = (index + 1) % items.length
  else if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') next = (index - 1 + items.length) % items.length
  else if (event.key === 'Home') next = 0
  else next = items.length - 1

  event.preventDefault()
  items[next].focus()
}

// Form navigation: Up/Down move between fields and buttons (Left/Right too on buttons and links).
// Left/Right and Home/End stay with text fields for caret movement. Select boxes keep native arrows.
export function handleFormKeyDown(event) {
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return
  const target = event.target
  if (!(target instanceof HTMLElement) || target.matches('select, textarea')) return

  const vertical = event.key === 'ArrowDown' || event.key === 'ArrowUp'
  const horizontal = event.key === 'ArrowLeft' || event.key === 'ArrowRight'
  if (!vertical && !(horizontal && !isTextField(target))) return

  const items = getFocusable(event.currentTarget)
  const index = items.indexOf(target)
  if (index === -1) return

  const step = event.key === 'ArrowDown' || event.key === 'ArrowRight' ? 1 : -1
  const next = items[index + step]
  if (!next) return

  event.preventDefault()
  next.focus()
}
