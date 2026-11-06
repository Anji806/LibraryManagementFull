import { useEffect, useRef, useState } from 'react'
import { getBookPreviewContent } from '../../data/bookPreviewContent.js'

function getBorrowStatus(book) {
  if (book.availableCopies > 0) return { label: 'Available', className: 'badge-active' }
  if (book.totalCopies > 0) return { label: 'Borrowed', className: 'badge-warning' }
  return { label: 'Unavailable', className: 'badge-neutral' }
}

function firstBookText(book, keys) {
  for (const key of keys) {
    const value = book[key]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return ''
}

const pages = [
  { key: 'introduction', label: 'Book Introduction' },
  { key: 'overview', label: 'About the Book' },
  { key: 'motto', label: 'Book Message' },
]

export function BookPreview({ book, borrowing = false, canBorrow = true, onBorrow }) {
  const status = getBorrowStatus(book)
  const researchedContent = getBookPreviewContent(book)
  const [currentPage, setCurrentPage] = useState(0)
  const readerRef = useRef(null)
  const pageRefs = useRef([])
  const introduction = researchedContent?.introduction || firstBookText(book, ['previewIntroduction', 'PreviewIntroduction', 'introduction', 'Introduction'])
  const previewTheme = researchedContent?.theme || firstBookText(book, ['previewTheme', 'PreviewTheme', 'mainTheme', 'MainTheme', 'theme', 'Theme'])
  const previewAbstract = researchedContent?.abstract || firstBookText(book, ['previewAbstract', 'PreviewAbstract', 'abstract', 'Abstract', 'storyOverview', 'StoryOverview', 'description', 'Description', 'summary', 'Summary'])
  const previewMotto = researchedContent?.motto || firstBookText(book, ['previewMotto', 'PreviewMotto', 'motto', 'Motto', 'centralMessage', 'CentralMessage'])
  const previewWhyRead = researchedContent?.whyRead || firstBookText(book, ['previewWhyRead', 'PreviewWhyRead', 'whyRead', 'WhyRead'])
  const previewAuthor = researchedContent?.author || book.authorName
  const previewYear = researchedContent?.year || book.publishedYear
  const previewGenre = researchedContent?.genre || book.categoryName

  useEffect(() => {
    if (!readerRef.current || !('IntersectionObserver' in window)) return undefined

    const observer = new IntersectionObserver((entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0]
      if (visible) setCurrentPage(Number(visible.target.dataset.pageIndex))
    }, { root: readerRef.current, threshold: [0.35, 0.6] })

    pageRefs.current.forEach((page) => page && observer.observe(page))
    return () => observer.disconnect()
  }, [book.bookId])

  function goToPage(index) {
    const boundedIndex = Math.max(0, Math.min(pages.length - 1, index))
    setCurrentPage(boundedIndex)
    pageRefs.current[boundedIndex]?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <div className="pdf-preview" style={{ '--book-accent': researchedContent?.accent ?? '#bb7950' }}>
      <div className="pdf-toolbar">
        <div className="pdf-file-label">
          <span className="pdf-file-icon" aria-hidden="true">PDF</span>
          <span><strong>{book.title}</strong><small>Three-page reader preview</small></span>
        </div>
        <span className={`badge ${status.className}`}>{status.label}</span>
      </div>

      <nav className="pdf-page-nav" aria-label="Preview pages">
        <button className="secondary-button pdf-nav-button" type="button" onClick={() => goToPage(currentPage - 1)} disabled={currentPage === 0}>
          <span aria-hidden="true">←</span> Previous
        </button>
        <span className="pdf-page-indicator" aria-live="polite">Page {currentPage + 1} of {pages.length}</span>
        <button className="secondary-button pdf-nav-button" type="button" onClick={() => goToPage(currentPage + 1)} disabled={currentPage === pages.length - 1}>
          Next <span aria-hidden="true">→</span>
        </button>
      </nav>

      <div className="pdf-reader" ref={readerRef}>
        {pages.map((page, index) => (
          <article
            className="pdf-page"
            id={`pdf-page-${page.key}`}
            data-page-index={index}
            ref={(element) => { pageRefs.current[index] = element }}
            key={page.key}
            aria-label={`Page ${index + 1}: ${page.label}`}
          >
            <div className="pdf-page-content">
              <div className="pdf-page-kicker">{page.label}</div>
              {page.key === 'introduction' && (
                <>
                  <h2 className="pdf-page-title">A closer look</h2>
                  <p className="pdf-page-lead">{book.title}</p>
                  <p className="pdf-page-byline">{previewAuthor} <span>·</span> {previewGenre}</p>
                  <div className="pdf-page-divider" />
                  <p className="pdf-page-byline">{previewYear ? `First published ${previewYear}` : 'Publication year not listed'}</p>
                  <h3>Introduction</h3>
                  <p>{introduction || 'A verified introduction has not been added to the catalog for this title yet.'}</p>
                  <h3>Main theme</h3>
                  <p>{previewTheme || 'A verified main theme has not been added to the catalog for this title yet.'}</p>
                </>
              )}
              {page.key === 'overview' && (
                <>
                  <h2 className="pdf-page-title">A glimpse inside</h2>
                  <p className="pdf-page-lead">Abstract &amp; story overview</p>
                  <div className="pdf-page-divider" />
                  <p className="pdf-abstract">{previewAbstract || 'A verified abstract has not been added to the catalog for this title yet.'}</p>
                  <h3>What makes it interesting?</h3>
                  <p>{previewWhyRead || 'A book-specific reading note has not been added to the catalog yet.'}</p>
                  <h3>What readers can take away</h3>
                  <p>{previewTheme || 'A verified main theme has not been added to the catalog for this title yet.'}</p>
                  <aside className="pdf-reading-note">
                    <span aria-hidden="true">✦</span>
                    <p>Story details are kept spoiler-free so the book’s discoveries remain yours.</p>
                  </aside>
                </>
              )}
              {page.key === 'motto' && (
                <div className="pdf-motto-page">
                  <div className="pdf-page-kicker">Promotional tagline · editorial paraphrase, not a quotation</div>
                  {previewMotto
                    ? <p className="pdf-motto-text">{previewMotto}</p>
                    : <p className="pdf-content-missing">A book-specific promotional tagline has not been added to the catalog yet.</p>}
                  <div className="pdf-page-divider" />
                  <h2>{book.title}</h2>
                  <p className="pdf-page-byline">by {previewAuthor}</p>
                  <h3>Central message</h3>
                  <p>{previewTheme || 'A verified central message has not been added to the catalog yet.'}</p>
                  <h3>Why choose this book?</h3>
                  <p>{previewWhyRead || 'A reader’s guide to this title has not been added to the catalog yet.'}</p>
                  <p className="pdf-motto-footer">A library preview · {previewGenre}</p>
                </div>
              )}
            </div>
            <div className="pdf-page-number">{String(index + 1).padStart(2, '0')} <span>/</span> {String(pages.length).padStart(2, '0')}</div>
          </article>
        ))}
      </div>

      <div className="pdf-footer">
        <div className="pdf-page-dots" aria-label="Select a preview page">
          {pages.map((page, index) => (
            <button
              key={page.key}
              className={`pdf-page-dot${currentPage === index ? ' is-current' : ''}`}
              type="button"
              aria-label={`Go to page ${index + 1}`}
              aria-current={currentPage === index ? 'page' : undefined}
              onClick={() => goToPage(index)}
            />
          ))}
        </div>
        {canBorrow && onBorrow && <div className="pdf-borrow-area">
            <span className="pdf-availability-note">{status.label} · {book.availableCopies} of {book.totalCopies} copies</span>
            <button className="primary-button pdf-borrow-button" type="button" disabled={book.availableCopies <= 0 || borrowing} onClick={onBorrow}>
              {borrowing ? 'Borrowing…' : 'Borrow book'}
            </button>
        </div>}
      </div>
    </div>
  )
}
