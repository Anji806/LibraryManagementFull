// Due dates are the end of a day in the library's time zone, so they are
// shown in that zone for every reader (matches the server's day counting).
const LIBRARY_TIME_ZONE = 'Asia/Kolkata'

export function formatDueDate(value) {
  if (!value) return '—'
  const hasZone = /(Z|[+-]\d\d:\d\d)$/.test(value)
  const date = new Date(hasZone ? value : `${value}Z`)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleDateString(undefined, { timeZone: LIBRARY_TIME_ZONE })
}

// The API returns UTC timestamps without a zone suffix.
export function formatDate(value) {
  if (!value) return '—'
  const hasZone = /(Z|[+-]\d\d:\d\d)$/.test(value)
  const date = new Date(hasZone ? value : `${value}Z`)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString()
}

