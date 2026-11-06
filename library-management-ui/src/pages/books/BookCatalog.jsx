import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getBooks } from '../../services/bookService.js'
import { borrowBook } from '../../services/borrowingService.js'
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
  const [borrowingId, setBorrowingId] = useState(null)
  const [detailsBook, setDetailsBook] = useState(null)
  const [previewBook, setPreviewBook] = useState(null)

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
        <input
          type="search"
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Search by title, author, category or year..."
          aria-label="Search books by title, author, category or year"
        />
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

