import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getBooks } from '../../services/bookService.js'
import { borrowBook } from '../../services/borrowingService.js'
import { ROLES } from '../../utils/roles.js'
import { MemberPicker } from '../members/MemberPicker.jsx'
import { MemberTransactions } from './MemberTransactions.jsx'

function BorrowForm({ memberId, onBorrowed }) {
  const toast = useToast()
  const fetchBooks = useCallback(() => getBooks(), [])
  const books = useApiResource(fetchBooks, 'Could not load books.')
  const [bookId, setBookId] = useState('')
  const [busy, setBusy] = useState(false)
  const available = (books.data ?? []).filter((book) => book.availableCopies > 0)

  const handleSubmit = useSingleFlight(submit)

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    try {
      await borrowBook(Number(bookId), memberId)
      toast.success('Book borrowed successfully.')
      setBookId('')
      books.reload()
      onBorrowed()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The book could not be borrowed.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      style={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'flex-end',
        gap: '10px',
        padding: '16px 18px',
        border: '1px solid var(--border)',
        borderRadius: 'var(--radius-lg)',
        background: 'var(--bg-card)',
        marginBottom: '20px',
      }}
    >
      <div className="field grow" style={{ margin: 0, minWidth: 'min(240px, 100%)' }}>
        <label htmlFor="borrow-book-select">Book to lend</label>
        <select
          id="borrow-book-select"
          value={bookId}
          onChange={(event) => setBookId(event.target.value)}
          required
          disabled={books.loading}
        >
          <option value="">
            {books.loading ? 'Loading books...' : available.length === 0 ? 'No books available' : 'Select an available book'}
          </option>
          {available.map((book) => (
            <option key={book.bookId} value={book.bookId}>
              {book.title} — {book.availableCopies} cop{book.availableCopies === 1 ? 'y' : 'ies'} available
            </option>
          ))}
        </select>
      </div>
      <button className="primary-button" type="submit" disabled={!bookId || busy} style={{ alignSelf: 'flex-end' }}>
        {busy ? 'Borrowing...' : 'Issue book'}
      </button>
      {books.error && <p className="form-error" role="alert" style={{ width: '100%', margin: 0 }}>{books.error}</p>}
    </form>
  )
}

// Members see their own loans; staff pick a member (and optionally lend a book).
export function Borrowing({ allowBorrow = true }) {
  const { role, user } = useAuth()
  const [memberId, setMemberId] = useState(null)
  const [refresh, setRefresh] = useState(0)

  if (role === ROLES.MEMBER) {
    return (
      <>
        <PageHeader
          title="My borrowed books"
          description="Books you currently have on loan."
          actions={<Link className="primary-button" to="/books">Browse catalog</Link>}
        />
        <MemberTransactions memberId={user.userId} mode="current" showReturn previewable />
      </>
    )
  }

  return (
    <>
      <PageHeader
        title={allowBorrow ? 'Borrowing' : 'Returns'}
        description={
          allowBorrow
            ? 'Select a member, choose an available book, and issue it.'
            : 'Select a member to process their book returns.'
        }
      />
      <MemberPicker value={memberId} onSelect={setMemberId} />
      {memberId ? (
        <>
          {allowBorrow && (
            <>
              <h2 className="section-title">Issue a book</h2>
              <BorrowForm memberId={memberId} onBorrowed={() => setRefresh((n) => n + 1)} />
            </>
          )}
          <h2 className="section-title">Current loans for this member</h2>
          <MemberTransactions key={`${memberId}-${refresh}`} memberId={memberId} mode="current" showReturn />
        </>
      ) : (
        <p className="muted-note">Select a member above to continue.</p>
      )}
    </>
  )
}
