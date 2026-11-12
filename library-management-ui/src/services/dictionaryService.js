import api from './api.js'

// Lookups are cached for the session so re-selecting a word is instant and
// does not hit the API again. Failures are not cached.
const cache = new Map()

export const NOT_FOUND = 'not-found'

/**
 * Resolves with the dictionary entry, or NOT_FOUND when the word is unknown.
 * Rejects for network/server problems.
 */
export async function lookupWord(word, { signal } = {}) {
  const key = word.toLowerCase()
  if (cache.has(key)) return cache.get(key)

  try {
    const response = await api.get(`/dictionary/${encodeURIComponent(word)}`, { signal })
    cache.set(key, response.data)
    return response.data
  } catch (error) {
    if (error?.response?.status === 404) {
      cache.set(key, NOT_FOUND)
      return NOT_FOUND
    }
    throw error
  }
}
