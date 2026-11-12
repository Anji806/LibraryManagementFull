import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { NOT_FOUND, lookupWord } from '../../services/dictionaryService.js'

const MARGIN = 8
const GAP = 10
const MAX_WIDTH = 340

// Small popup that shows the meaning of one word next to where it was selected.
// `anchor` is the selection's client rectangle; the popup flips above the word
// or slides sideways so it never leaves the screen.
export function DictionaryPopup({ word, anchor, onClose }) {
  const popupRef = useRef(null)
  const audioRef = useRef(null)
  const [state, setState] = useState({ status: 'loading' })
  const [position, setPosition] = useState({ top: -9999, left: -9999 })
  const [playing, setPlaying] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    let active = true

    lookupWord(word, { signal: controller.signal })
      .then((result) => {
        if (!active) return
        setState(result === NOT_FOUND ? { status: 'not-found' } : { status: 'ready', entry: result })
      })
      .catch((error) => {
        if (active && error?.code !== 'ERR_CANCELED') setState({ status: 'error' })
      })

    return () => {
      active = false
      controller.abort()
      audioRef.current?.pause()
    }
  }, [word])

  // Re-measure whenever the content changes size (loading -> result).
  useLayoutEffect(() => {
    const popup = popupRef.current
    if (!popup) return

    const { innerWidth, innerHeight } = window
    const width = Math.min(MAX_WIDTH, innerWidth - MARGIN * 2)
    const height = popup.offsetHeight
    const center = anchor.left + anchor.width / 2

    const left = Math.max(MARGIN, Math.min(center - width / 2, innerWidth - width - MARGIN))

    const below = anchor.bottom + GAP
    const above = anchor.top - GAP - height
    let top = below
    if (below + height > innerHeight - MARGIN && above >= MARGIN) top = above
    top = Math.max(MARGIN, Math.min(top, innerHeight - height - MARGIN))

    setPosition({ top, left, width })
  }, [anchor, state])

  function playAudio(url) {
    audioRef.current?.pause()
    const audio = new Audio(url)
    audioRef.current = audio
    audio.onended = () => setPlaying(false)
    audio.onerror = () => setPlaying(false)
    setPlaying(true)
    audio.play().catch(() => setPlaying(false))
  }

  const entry = state.status === 'ready' ? state.entry : null

  return createPortal(
    <div
      ref={popupRef}
      className="dictionary-popup"
      data-dictionary-popup
      role="dialog"
      aria-label={`Meaning of ${word}`}
      style={{ top: position.top, left: position.left, width: position.width }}
      onMouseDown={(event) => event.stopPropagation()}
    >
      <div className="dictionary-header">
        <div>
          <strong className="dictionary-word">{entry?.word ?? word}</strong>
          {entry?.phonetic && <span className="dictionary-phonetic">{entry.phonetic}</span>}
        </div>
        <div className="dictionary-header-actions">
          {entry?.audioUrl && (
            <button
              className="dictionary-icon-button"
              type="button"
              onClick={() => playAudio(entry.audioUrl)}
              aria-label={`Hear how “${entry.word}” is pronounced`}
              disabled={playing}
            >
              {playing ? '…' : '🔊'}
            </button>
          )}
          <button
            className="dictionary-icon-button"
            type="button"
            onClick={onClose}
            aria-label="Close meaning"
          >
            ×
          </button>
        </div>
      </div>

      {state.status === 'loading' && (
        <p className="dictionary-status" role="status">
          <span className="dictionary-spinner" aria-hidden="true" /> Looking up the meaning…
        </p>
      )}

      {state.status === 'not-found' && (
        <p className="dictionary-status">
          Sorry, we couldn’t find a meaning for “{word}”. Try another word.
        </p>
      )}

      {state.status === 'error' && (
        <p className="dictionary-status">
          The dictionary isn’t available right now. Please try again in a moment.
        </p>
      )}

      {entry && (
        <div className="dictionary-body">
          {entry.meanings.map((meaning) => (
            <section key={meaning.partOfSpeech || 'other'} className="dictionary-meaning">
              {meaning.partOfSpeech && <span className="dictionary-pos">{meaning.partOfSpeech}</span>}
              <ol>
                {meaning.definitions.map((item) => (
                  <li key={item.definition}>
                    <span>{item.definition}</span>
                    {item.example && <em className="dictionary-example">“{item.example}”</em>}
                  </li>
                ))}
              </ol>
            </section>
          ))}
          <p className="dictionary-source">
            Source: <a href="https://dictionaryapi.dev" target="_blank" rel="noreferrer">Free Dictionary API</a> (Wiktionary, CC BY-SA)
          </p>
        </div>
      )}
    </div>,
    document.body,
  )
}
