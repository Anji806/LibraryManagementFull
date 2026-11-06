import { useCallback, useEffect, useRef } from 'react'

// Wraps an async handler so a second call is ignored while the first is still running.
// Guards against double submits from rapid Enter/Space presses before React re-renders.
export function useSingleFlight(handler) {
  const running = useRef(false)
  const latest = useRef(handler)
  useEffect(() => {
    latest.current = handler
  })

  return useCallback(async (...args) => {
    if (running.current) return undefined
    running.current = true
    try {
      return await latest.current(...args)
    } finally {
      running.current = false
    }
  }, [])
}
