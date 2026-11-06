import axios from 'axios'
import { getStoredToken } from '../utils/auth.js'

const baseURL = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/+$/, '')

const api = axios.create({
  baseURL,
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  },
})

api.interceptors.request.use((config) => {
  if (!config.skipAuth) {
    const token = getStoredToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.dispatchEvent(new Event('auth:unauthorized'))
    }

    return Promise.reject(error)
  },
)

export function getApiErrorMessage(error, fallback = 'The request could not be completed.') {
  const status = error?.response?.status

  if (!status) return 'Could not connect to the library service. Check the server and try again.'
  if (status === 401) return 'Your session is no longer valid. Please sign in again.'
  if (status === 403) return 'You do not have permission to perform this action.'
  if (status === 404) return error.response.data?.message || 'The requested record was not found. It may already have been removed.'
  if (status === 409) return error.response.data?.message || 'This request conflicts with the current library data.'
  if (status === 500) return error.response.data?.message || 'The library service encountered an error. Please try again later.'
  if (status === 405) return 'This API operation is unavailable on the running server. Restart or update the API, then try again.'

  if (status === 400) {
    const data = error.response.data
    if (typeof data?.message === 'string') return data.message
    if (typeof data?.title === 'string') return data.title
    if (data?.errors && typeof data.errors === 'object') {
      const firstError = Object.values(data.errors).flat().find(Boolean)
      if (firstError) return firstError
    }

    return 'Please check the submitted information and try again.'
  }

  return error.response.data?.message || fallback
}

export default api
