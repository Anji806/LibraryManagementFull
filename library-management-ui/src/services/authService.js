import api from './api.js'

export async function login(credentials) {
  const response = await api.post('/auth/login', credentials, { skipAuth: true })
  return response.data
}

export async function register(details) {
  const response = await api.post('/auth/register', details, { skipAuth: true })
  return response.data
}

export async function logout() {
  return api.post('/auth/logout')
}
