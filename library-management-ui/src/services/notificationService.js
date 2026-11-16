import api from './api.js'

// Admin only: the log of notification emails and what happened to each.
export async function getNotifications({ type, status, page = 1, pageSize = 25 } = {}) {
  const response = await api.get('/notifications', {
    params: { type: type || undefined, status: status || undefined, page, pageSize },
  })
  return response.data
}

export async function getNotificationSummary() {
  const response = await api.get('/notifications/summary')
  return response.data
}

// Runs the due-soon / overdue / availability checks right now instead of waiting for the scheduler.
export async function runNotificationChecks() {
  const response = await api.post('/notifications/run-now')
  return response.data
}
