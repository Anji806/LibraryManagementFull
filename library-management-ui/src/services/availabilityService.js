import api from './api.js'

// "Email me when this unavailable book can be borrowed" (members only).
export async function requestAvailabilityNotice(bookId) {
  const response = await api.post('/availability-requests', { bookId })
  return response.data
}

export async function cancelAvailabilityNotice(bookId) {
  await api.delete(`/availability-requests/${bookId}`)
}

export async function getMyAvailabilityNotices() {
  const response = await api.get('/availability-requests/mine')
  return response.data
}
