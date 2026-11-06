import { useCallback, useEffect, useState } from 'react'
import { getApiErrorMessage } from '../services/api.js'

// Loads data from a fetcher and exposes loading/error state plus a reload.
// The fetcher must be stable (module-level function or useCallback).
export function useApiResource(fetcher, fallbackMessage = 'Could not load data.') {
  const [state, setState] = useState({ data: null, loading: true, error: '' })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let cancelled = false

    fetcher()
      .then((data) => {
        if (!cancelled) setState({ data, loading: false, error: '' })
      })
      .catch((error) => {
        if (!cancelled) {
          setState({ data: null, loading: false, error: getApiErrorMessage(error, fallbackMessage) })
        }
      })

    return () => {
      cancelled = true
    }
  }, [fetcher, fallbackMessage, version])

  const reload = useCallback(() => {
    setState((current) => ({ ...current, loading: true, error: '' }))
    setVersion((current) => current + 1)
  }, [])

  return { ...state, reload }
}
