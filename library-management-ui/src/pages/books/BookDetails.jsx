import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  EmptyState,
  PageHeader,
  ResourceView,
} from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getBookBorrowHistory, getBookById } from '../../services/bookService.js'
import { borrowBook } from '../../services/borrowingService.js'
import { ROLES, isStaff } from '../../utils/roles.js'
import { TransactionTable } from '../borrowing/TransactionTable.jsx'
import { BookPreview } from '../../components/books/BookPreview.jsx'

function BookHistory({ bookId }) {
  const fetchHistory = useCallback(() => getBookBorrowHistory(bookId), [bookId])
  const history = useApiResource(fetchHistory, 'Could not load borrowing history.')

  return (
    <>
      <h2 className="section-title">Borrowing history</h2>
      <ResourceView
        resource={history}
        isEmpty={!history.data?.length}
        empty={<EmptyState title="No borrowing history" message="This book has never been borrowed." />}
      >
        <TransactionTable transactions={history.data ?? []} showMember />
      </ResourceView>
    </>
  )
}

export function BookDetails() {
  const { id } = useParams()
  const { role, user } = useAuth()
  const toast = useToast()
  const [borrowing, setBorrowing] = useState(false)

  const fetchBook = useCallback(() => getBookById(id), [id])
  const book = useApiResource(fetchBook, 'Could not load this book.')

  async function handleBorrow() {
    setBorrowing(true)
    try {
      await borrowBook(book.data.bookId, user.userId)
      toast.success(`You borrowed "${book.data.title}".`)
      book.reload()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The book could not be borrowed.'))
    } finally {
      setBorrowing(false)
    }
  }

  const data = book.data

  return (
    <>
      <PageHeader
        title={data?.title ?? 'Book details'}
        actions={<Link className="secondary-button" to="/books">← Back to catalog</Link>}
      />
      <ResourceView resource={book} isEmpty={!data}>
        {data && (
          <>
            <BookPreview
              key={data.bookId}
              book={data}
              canBorrow={role === ROLES.MEMBER}
              borrowing={borrowing}
              onBorrow={handleBorrow}
            />
            <section className="panel book-facts">
              <h2 className="section-title">Book details</h2>
              <dl className="detail-list">
                <div><dt>Author</dt><dd>{data.authorName}</dd></div>
                <div><dt>Category</dt><dd>{data.categoryName}</dd></div>
                <div><dt>ISBN</dt><dd><span style={{ fontFamily: 'monospace' }}>{data.isbn}</span></dd></div>
                <div><dt>Published</dt><dd>{data.publishedYear}</dd></div>
                <div><dt>Total copies</dt><dd>{data.totalCopies}</dd></div>
              </dl>
            </section>
            {isStaff(role) && <BookHistory bookId={data.bookId} />}
          </>
        )}
      </ResourceView>
    </>
  )
}
