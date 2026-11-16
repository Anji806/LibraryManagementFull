const DEVICE_ID_KEY = 'library-management.device-id'

function randomId() {
  if (window.crypto?.randomUUID) return window.crypto.randomUUID()
  const bytes = window.crypto.getRandomValues(new Uint8Array(16))
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
}

// Identifies this browser profile (not a person or a physical machine). The
// server never trusts it as proof of identity; it only helps recognise a
// repeat sign-in from the same browser.
export function getDeviceId() {
  try {
    let id = window.localStorage.getItem(DEVICE_ID_KEY)
    if (!id) {
      id = randomId()
      window.localStorage.setItem(DEVICE_ID_KEY, id)
    }
    return id
  } catch {
    // Storage blocked: fall back to an id that lasts for this page load.
    return randomId()
  }
}
