import { useState } from 'react'
import { ConfirmDialog } from '../../components/ui/Modal.jsx'
import { StatusBadge } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { returnBook } from '../../services/borrowingService.js'
import { formatDate } from '../../utils/date.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'

// Table of borrow transactions. When `onChanged` is given, active loans get a
// Return action (the API restricts Members to their own transactions).
export function TransactionTable({ transactions, onChanged, showMember = false, onPreviewBook }) {
  const toast = useToast()
  const [pending, setPending] = useState(null)
  const [busy, setBusy] = useState(false)

  const handleReturn = useSingleFlight(async () => {
    setBusy(true)
    try {
      await returnBook(pending.transactionId)
      toast.success(`"${pending.book?.title ?? 'Book'}" returned.`)
      setPending(null)
      onChanged?.()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The book could not be returned.'))
      setPending(null)
    } finally {
      setBusy(false)
    }
  })

  return (
    <>
      <div className="table-wrap">
        <table className="data-table" aria-label="Borrowing transactions" onKeyDown={handleTableKeyDown}>
          <thead>
            <tr>
              <th>Book</th>
              {showMember && <th>Member</th>}
              <th>Borrowed</th>
              <th>Returned</th>
              <th>Status</th>
              {onChanged && <th aria-label="Actions" />}
            </tr>
          </thead>
          <tbody>
            {transactions.map((transaction) => (
              <tr key={transaction.transactionId}>
                <td data-label="Book">
                  {onPreviewBook && transaction.book ? (
                    <button
                      className="book-title-button"
                      type="button"
                      onClick={() => onPreviewBook(transaction.book)}
                      aria-label={`View book details for ${transaction.book.title}`}
                    >
                      <strong>{transaction.book.title}</strong>
                      <span>View book details</span>
                    </button>
                  ) : (
                    <strong>{transaction.book?.title ?? `Book #${transaction.bookId}`}</strong>
                  )}
                  {transaction.book?.author?.authorName && (
                    <span className="cell-sub">{transaction.book.author.authorName}</span>
                  )}
                </td>
                {showMember && (
                  <td data-label="Member">
                    {transaction.member?.fullName ?? `Member #${transaction.memberId}`}
                  </td>
                )}
                <td data-label="Borrowed">{formatDate(transaction.borrowDate)}</td>
                <td data-label="Returned">{formatDate(transaction.returnDate)}</td>
                <td data-label="Status"><StatusBadge status={transaction.status} /></td>
                {onChanged && (
                  <td className="row-actions">
                    {transaction.status !== 'Returned' && (
                      <button
                        className="secondary-button small"
                        type="button"
                        onClick={() => setPending(transaction)}
                      >
                        Return
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {pending && (
        <ConfirmDialog
          title="Return this book?"
          message={`Mark "${pending.book?.title ?? 'this book'}" as returned. This will restore one copy to available inventory.`}
          confirmLabel="Return book"
          busy={busy}
          onConfirm={handleReturn}
          onCancel={() => setPending(null)}
        />
      )}
    </>
  )
}
