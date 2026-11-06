// The API returns UTC timestamps without a zone suffix.
export function formatDate(value) {
  if (!value) return '—'
  const hasZone = /(Z|[+-]\d\d:\d\d)$/.test(value)
  const date = new Date(hasZone ? value : `${value}Z`)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString()
}

