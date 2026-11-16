import { useCallback, useEffect, useMemo, useState } from 'react'
import * as authService from '../services/authService.js'
import { AUTH_STORAGE_KEY, createSession, readSession } from '../utils/auth.js'
import { AuthContext } from './authContext.js'

export function AuthProvider({ children }) {
  const [sessionExpired, setSessionExpired] = useState(false)
  const [initialState] = useState(() => {
    try {
      return { session: readSession(), error: '' }
    } catch {
      return {
        session: null,
        error: 'Your sign-in session could not be restored. Please reload the page.',
      }
    }
  })
  const [session, setSession] = useState(initialState.session)
  const isLoading = false
  const initializationError = initialState.error

  const acceptAuthResponse = useCallback((response) => {
    const nextSession = createSession(response)
    window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(nextSession))
    setSession(nextSession)
    setSessionExpired(false)
    return nextSession.user
  }, [])

  const login = useCallback(
    async (credentials) => acceptAuthResponse(await authService.login(credentials)),
    [acceptAuthResponse],
  )

  const completeLogin = useCallback(
    async (challenge) => acceptAuthResponse(await authService.completeLogin(challenge)),
    [acceptAuthResponse],
  )

  const register = useCallback(
    async (details) => acceptAuthResponse(await authService.register(details)),
    [acceptAuthResponse],
  )

  const logout = useCallback(async (notifyServer = true) => {
    if (notifyServer && session?.token) {
      try { await authService.logout() } catch { /* Keep local sign-out available if the API is offline. */ }
    }
    window.localStorage.removeItem(AUTH_STORAGE_KEY)
    setSession(null)
  }, [session])

  useEffect(() => {
    // ProtectedRoute performs the redirect once the session is cleared.
    const handleUnauthorized = () => {
      logout(false)
      setSessionExpired(true)
    }

    window.addEventListener('auth:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('auth:unauthorized', handleUnauthorized)
  }, [logout])

  const value = useMemo(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      role: session?.user?.role ?? null,
      isAuthenticated: Boolean(session?.token),
      isLoading,
      initializationError,
      sessionExpired,
      login,
      completeLogin,
      register,
      logout,
    }),
    [initializationError, isLoading, login, completeLogin, logout, register, session, sessionExpired],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
