import { getStoredToken } from '../utils/auth.js'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/+$/, '')

function waitForReconnect(delay, signal) {
  return new Promise((resolve) => {
    const timer = window.setTimeout(resolve, delay)
    signal.addEventListener('abort', () => {
      window.clearTimeout(timer)
      resolve()
    }, { once: true })
  })
}

function dispatchFrame(frame, onEvent) {
  let eventName = 'message'
  const data = []
  for (const line of frame.split(/\r?\n/)) {
    if (line.startsWith('event:')) eventName = line.slice(6).trim()
    else if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
  }
  if (!data.length) return
  try { onEvent(eventName, JSON.parse(data.join('\n'))) } catch { /* Ignore malformed frames and keep the stream alive. */ }
}

export async function runLoginActivityStream({ onEvent, onConnectionChange, signal }) {
  let retryDelay = 1000
  const eventUrl = new URL(`${apiBaseUrl}/login-history/events`, window.location.origin)

  while (!signal.aborted) {
    try {
      const token = getStoredToken()
      if (!token) return
      const response = await fetch(eventUrl, {
        method: 'GET',
        headers: { Authorization: `Bearer ${token}`, Accept: 'text/event-stream' },
        cache: 'no-store',
        signal,
      })

      if (response.status === 401) {
        window.dispatchEvent(new Event('auth:unauthorized'))
        return
      }
      if (response.status === 403) return
      if (!response.ok || !response.body) throw new Error(`Activity stream returned HTTP ${response.status}.`)

      retryDelay = 1000
      onConnectionChange(true)
      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffered = ''
      while (!signal.aborted) {
        const { done, value } = await reader.read()
        if (done) break
        buffered += decoder.decode(value, { stream: true })
        let boundary
        while ((boundary = buffered.search(/\r?\n\r?\n/)) >= 0) {
          const frame = buffered.slice(0, boundary)
          const separator = buffered.slice(boundary).match(/^\r?\n\r?\n/)[0]
          buffered = buffered.slice(boundary + separator.length)
          dispatchFrame(frame, onEvent)
        }
      }
      if (signal.aborted) return
      throw new Error('Activity stream ended; reconnecting.')
    } catch {
      if (signal.aborted) return
      onConnectionChange(false)
      await waitForReconnect(retryDelay, signal)
      retryDelay = Math.min(retryDelay * 2, 15000)
    }
  }
}
