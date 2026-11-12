import api from './api.js'
import { getDeviceId } from '../utils/deviceId.js'

export async function login(credentials) {
  const response = await api.post(
    '/auth/login',
    { ...credentials, deviceId: getDeviceId() },
    { skipAuth: true },
  )
  return response.data
}

// Finishes a sign-in that was held at the two-device limit: the chosen
// device is logged out and this one receives its own token.
export async function completeLogin({ challengeToken, sessionToRevokeId }) {
  const response = await api.post(
    '/auth/complete-login',
    { challengeToken, sessionToRevokeId },
    { skipAuth: true },
  )
  return response.data
}

export async function register(details) {
  const response = await api.post(
    '/auth/register',
    { ...details, deviceId: getDeviceId() },
    { skipAuth: true },
  )
  return response.data
}

export async function logout() {
  return api.post('/auth/logout')
}
