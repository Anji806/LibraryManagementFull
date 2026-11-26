import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getBooks } from '../../services/bookService.js'
import { borrowBook } from '../../services/borrowingService.js'
import { getMyAvailabilityNotices, requestAvailabilityNotice } from '../../services/availabilityService.js'
import { Modal } from '../../components/ui/Modal.jsx'
import { BookPreview } from '../../components/books/BookPreview.jsx'
import { filterBooks } from '../../utils/books.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'
import { PERMISSIONS, ROLES, hasPermission } from '../../utils/roles.js'
import { getBookPreviewContent } from '../../data/bookPreviewContent.js'
import { BookCatalogDetails } from '../../components/books/BookCatalogDetails.jsx'

export function BookCatalog() {
  const { role, user } = useAuth()
  const toast = useToast()
  const [input, setInput] = useState('')
  const [query, setQuery] = useState('')
  const [allTitles, setAllTitles] = useState([])
  const [focused, setFocused] = useState(false)
  const [slide, setSlide] = useState(0)
  const [borrowingId, setBorrowingId] = useState(null)
  const [detailsBook, setDetailsBook] = useState(null)
  const [previewBook, setPreviewBook] = useState(null)
  // Books this member asked to be emailed about, and the one being saved.
  const [waitingIds, setWaitingIds] = useState(() => new Set())
  const [notifyingId, setNotifyingId] = useState(null)

  useEffect(() => {
    if (role !== ROLES.MEMBER) return undefined
    let active = true
    getMyAvailabilityNotices()
      .then((requests) => {
        if (active) setWaitingIds(new Set(requests.filter((r) => r.status === 'Waiting').map((r) => r.bookId)))
      })
      .catch(() => { /* the button simply starts enabled */ })
    return () => { active = false }
  }, [role])

  // Titles of available books, loaded once, for the scrolling search-bar ticker.
  useEffect(() => {
    let active = true
    getBooks()
      .then((all) => {
        if (active) setAllTitles([...new Set(all.filter((b) => b.availableCopies > 0).map((b) => b.title))])
      })
      .catch(() => { /* the ticker is optional */ })
    return () => { active = false }
  }, [])

  // Roll the ticker up one title every few seconds.
  useEffect(() => {
    if (allTitles.length < 2) return undefined
    const timer = setInterval(() => setSlide((s) => (s >= allTitles.length ? s : s + 1)), 2500)
    return () => clearInterval(timer)
  }, [allTitles.length])

  const fetchBooks = useCallback(
    async () => filterBooks(await getBooks(), query),
    [query],
  )
  const books = useApiResource(fetchBooks, 'Could not load books.')

  function handleSearch(event) {
    event.preventDefault()
    setQuery(input.trim())
  }

  const handleBorrow = useSingleFlight(borrow)

  async function borrow(book) {
    setBorrowingId(book.bookId)
    try {
      await borrowBook(book.bookId, user.userId)
      toast.success(`You borrowed "${book.title}".`)
      setDetailsBook(null)
      setPreviewBook(null)
      books.reload()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The book could not be borrowed.'))
    } finally {
      setBorrowingId(null)
    }
  }

  async function notifyWhenAvailable(book) {
    setNotifyingId(book.bookId)
    try {
      await requestAvailabilityNotice(book.bookId)
      setWaitingIds((current) => new Set(current).add(book.bookId))
      toast.success(`We'll email you when "${book.title}" is available.`)
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The request could not be saved.'))
    } finally {
      setNotifyingId(null)
    }
  }

  const canManage = hasPermission(role, PERMISSIONS.MANAGE_BOOKS)

  return (
    <>
      <PageHeader
        title="Book catalog"
        description="Search and browse books held by the library."
        actions={
          canManage && (
            <Link className="primary-button" to="/books/manage">Manage books</Link>
          )
        }
      />
      <form className="toolbar" onSubmit={handleSearch} role="search">
        <div className="search-suggest">
          <input
            type="search"
            value={input}
            onChange={(event) => setInput(event.target.value)}
            onFocus={() => setFocused(true)}
            onBlur={() => setFocused(false)}
            placeholder={allTitles.length && !focused ? '' : 'Search by title, author, category or year...'}
            aria-label="Search books by title, author, category or year"
          />
          {allTitles.length > 0 && !focused && !input && (
            <div className="search-ticker" aria-hidden="true">
              <span className="search-ticker-label">Explore the</span>
              <div className="search-ticker-window">
                <div
                  className="search-ticker-track"
                  style={{
                    transform: `translateY(-${slide * 40}px)`,
                    transition: slide === 0 ? 'none' : undefined,
                  }}
                  onTransitionEnd={() => { if (slide >= allTitles.length) setSlide(0) }}
                >
                  {/* the first title is repeated so the roll loops without a jump */}
                  {[...allTitles, allTitles[0]].map((title, i) => (
                    <strong className="search-ticker-item" key={i}>&ldquo;{title}&rdquo;</strong>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>
        <button className="primary-button" type="submit">Search</button>
        {query && (
          <button
            className="secondary-button"
            type="button"
            onClick={() => { setInput(''); setQuery('') }}
          >
            Clear
          </button>
        )}
      </form>
      <ResourceView
        resource={books}
        isEmpty={!books.data?.length}
        empty={
          <EmptyState
            title={query ? 'No matching books' : 'No books yet'}
            message={query ? `Nothing matched "${query}".` : 'The catalog is empty.'}
          />
        }
      >
        <div className="table-wrap">
          <table className="data-table" aria-label="Book catalog" onKeyDown={handleTableKeyDown}>
            <thead>
              <tr>
                <th>Title</th>
                <th>Author</th>
                <th>Category</th>
                <th>ISBN</th>
                <th>Year</th>
                <th>Availability</th>
              </tr>
            </thead>
            <tbody>
              {books.data?.map((book) => (
                <tr key={book.bookId}>
                  <td data-label="Title">
                    <button
                      className="book-title-button"
                      type="button"
                      onClick={() => setDetailsBook(book)}
                      aria-label={`View book details for ${book.title}`}
                    >
                      <strong>{book.title}</strong>
                      <span>View book details</span>
                    </button>
                  </td>
                  <td data-label="Author">{getBookPreviewContent(book)?.author ?? book.authorName}</td>
                  <td data-label="Category">{book.categoryName}</td>
                  <td data-label="ISBN">
                    <span style={{ fontFamily: 'monospace', fontSize: '12.5px', color: 'var(--text-muted)' }}>
                      {book.isbn}
                    </span>
                  </td>
                  <td data-label="Year">{book.publishedYear}</td>
                  <td data-label="Availability">
                    <span className={`badge ${book.availableCopies > 0 ? 'badge-active' : 'badge-neutral'}`}>
                      {book.availableCopies}/{book.totalCopies} avail.
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ResourceView>
      {detailsBook && (
        <Modal
          title="Book details"
          onClose={() => setDetailsBook(null)}
        >
          <BookCatalogDetails
            book={detailsBook}
            canBorrow={role === ROLES.MEMBER}
            borrowing={borrowingId === detailsBook.bookId}
            onBorrow={() => handleBorrow(detailsBook)}
            onNotify={() => notifyWhenAvailable(detailsBook)}
            notifying={notifyingId === detailsBook.bookId}
            waiting={waitingIds.has(detailsBook.bookId)}
            onPreview={() => {
              setPreviewBook(detailsBook)
              setDetailsBook(null)
            }}
          />
        </Modal>
      )}
      {previewBook && (
        <Modal
          title="Book preview"
          onClose={() => setPreviewBook(null)}
        >
          <BookPreview
            key={previewBook.bookId}
            book={previewBook}
          />
        </Modal>
      )}
    </>
  )
}

