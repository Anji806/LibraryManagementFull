import api from './api.js'

// Admin-only diagnostic: asks the API to send a test message through Gmail SMTP.
export async function sendTestEmail(toEmail) {
  const response = await api.post('/email/test', { toEmail })
  return response.data
}
