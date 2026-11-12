import { useCallback, useEffect, useRef, useState } from 'react'

const INTERACTIVE = 'button, a, input, textarea, select, summary, [role="button"], [data-dictionary-popup]'
const SINGLE_WORD = /^\p{L}[\p{L}'-]*$/u
const MAX_WORD_LENGTH = 40
const TOUCH_SETTLE_MS = 450

// Strips punctuation/quotes around a selection ("reader," -> "reader") and
// accepts only one word. Returns null for anything else.
export function cleanSelectedWord(text) {
  const trimmed = text.trim()
  if (!trimmed || /\s/.test(trimmed)) return null

  const word = trimmed
    .replace(/’/g, "'")
    .replace(/^[^\p{L}]+|[^\p{L}]+$/gu, '')

  return word.length <= MAX_WORD_LENGTH && SINGLE_WORD.test(word) ? word : null
}

/**
 * Reports the single word the reader selected inside `containerRef` (and
 * nowhere else) together with its on-screen rectangle.
 * Works for mouse (pointer up) and touch (selection handles, settled).
 */
export function useWordSelection(containerRef) {
  const [selection, setSelection] = useState(null)
  const lastKey = useRef('')
  const pointerType = useRef('mouse')
  const settleTimer = useRef(0)

  const clear = useCallback(() => {
    lastKey.current = ''
    setSelection(null)
  }, [])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return undefined

    function evaluate() {
      const current = window.getSelection()
      if (!current || current.rangeCount === 0 || current.isCollapsed) return

      const range = current.getRangeAt(0)
      const anchor = range.commonAncestorContainer
      const element = anchor.nodeType === Node.ELEMENT_NODE ? anchor : anchor.parentElement

      // Only text inside the reading area, never buttons, links or navigation.
      if (!element || !container.contains(element) || element.closest(INTERACTIVE)) return

      const word = cleanSelectedWord(current.toString())
      if (!word) return

      const rect = range.getClientRects()[0] ?? range.getBoundingClientRect()
      if (!rect || (rect.width === 0 && rect.height === 0)) return

      // The same selection must not reopen the popup again and again.
      const key = `${word}|${Math.round(rect.left)}|${Math.round(rect.top)}`
      if (key === lastKey.current) return
      lastKey.current = key

      setSelection({
        word,
        rect: { top: rect.top, bottom: rect.bottom, left: rect.left, right: rect.right, width: rect.width },
      })
    }

    function handlePointerDown(event) {
      pointerType.current = event.pointerType || 'mouse'
    }

    function handlePointerUp(event) {
      if (event.target instanceof Element && event.target.closest(INTERACTIVE)) return
      // Touch selections are finished through selectionchange below.
      if (event.pointerType === 'touch') return
      window.setTimeout(evaluate, 0)
    }

    // Touch: the selection handles move after pointerup, so wait until it settles.
    function handleSelectionChange() {
      if (pointerType.current !== 'touch') return
      window.clearTimeout(settleTimer.current)
      settleTimer.current = window.setTimeout(evaluate, TOUCH_SETTLE_MS)
    }

    // Anything outside the popup dismisses it (a new selection starts fresh).
    function handleOutsidePointerDown(event) {
      if (event.target instanceof Element && event.target.closest('[data-dictionary-popup]')) return
      clear()
    }

    container.addEventListener('pointerdown', handlePointerDown)
    container.addEventListener('pointerup', handlePointerUp)
    container.addEventListener('scroll', clear)
    document.addEventListener('selectionchange', handleSelectionChange)
    document.addEventListener('pointerdown', handleOutsidePointerDown, true)
    window.addEventListener('resize', clear)

    return () => {
      window.clearTimeout(settleTimer.current)
      container.removeEventListener('pointerdown', handlePointerDown)
      container.removeEventListener('pointerup', handlePointerUp)
      container.removeEventListener('scroll', clear)
      document.removeEventListener('selectionchange', handleSelectionChange)
      document.removeEventListener('pointerdown', handleOutsidePointerDown, true)
      window.removeEventListener('resize', clear)
    }
  }, [containerRef, clear])

  return { selection, clear }
}
