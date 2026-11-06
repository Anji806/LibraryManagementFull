import { ALL_ROLES } from './roles.js'

export const AUTH_STORAGE_KEY = 'library-management.session'

const roleClaimNames = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role',
]

const nameClaimNames = [
  'name',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
]

const idClaimNames = [
  'nameid',
  'sub',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
]

function claimValue(payload, claimNames) {
  for (const name of claimNames) {
    const value = payload[name]
    if (Array.isArray(value)) return value[0]
    if (value) return value
  }

  return undefined
}

function decodeTokenPayload(token) {
  const segments = token.split('.')
  if (segments.length !== 3) return null

  try {
    const base64 = segments[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const binary = atob(padded)
    const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0))
    return JSON.parse(new TextDecoder().decode(bytes))
  } catch {
    return null
  }
}

export function createSession(response) {
  const token = response?.token ?? response?.Token
  if (typeof token !== 'string' || !token) {
    throw new Error('The authentication response did not include a token.')
  }

  const claims = decodeTokenPayload(token)
  if (!claims) {
    throw new Error('The authentication token could not be read.')
  }

  const role = response.role ?? response.Role ?? claimValue(claims, roleClaimNames)
  const expiresAt = Number(claims.exp)
  if (!role || !Number.isFinite(expiresAt) || expiresAt * 1000 <= Date.now()) {
    throw new Error('The authentication response did not contain a valid session.')
  }

  return {
    token,
    expiresAt,
    user: {
      userId: response.userId ?? response.UserId ?? claimValue(claims, idClaimNames),
      fullName:
        response.fullName ??
        response.FullName ??
        claimValue(claims, nameClaimNames) ??
        'Library user',
      email:
        response.email ??
        response.Email ??
        claims.email ??
        claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ??
        '',
      role,
    },
  }
}

export function readSession() {
  const storedValue = window.localStorage.getItem(AUTH_STORAGE_KEY)
  if (!storedValue) return null

  let session
  try {
    session = JSON.parse(storedValue)
  } catch {
    window.localStorage.removeItem(AUTH_STORAGE_KEY)
    return null
  }

  if (
    typeof session?.token !== 'string' ||
    !session.token ||
    !session.user?.role ||
    !Number.isFinite(session.expiresAt) ||
    session.expiresAt * 1000 <= Date.now()
  ) {
    window.localStorage.removeItem(AUTH_STORAGE_KEY)
    return null
  }

  return session
}

export function getStoredToken() {
  return readSession()?.token ?? null
}

export function isKnownRole(role) {
  return ALL_ROLES.includes(role)
}
