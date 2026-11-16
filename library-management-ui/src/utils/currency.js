// Amounts come from the API in rupees (calculated on the server).
const rupees = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

export function formatCurrency(amount) {
  return rupees.format(Number(amount) || 0)
}
