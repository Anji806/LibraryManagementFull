import { useState } from 'react'
import { ConfirmDialog } from '../../components/ui/Modal.jsx'
import { StatusBadge } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { returnBook } from '../../services/borrowingService.js'
import { formatCurrency } from '../../utils/currency.js'
import { formatDate, formatDueDate } from '../../utils/date.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'

function dueNote(transaction) {
  if (transaction.returnDate || !transaction.dueDate) return ''
  if (transaction.isOverdue) {
    const days = transaction.overdueDays
    return `${days} day${days === 1 ? '' : 's'} overdue`
  }
  const left = transaction.daysRemaining
  if (left === 0) return 'Due today'
  return left > 0 ? `${left} day${left === 1 ? '' : 's'} left` : ''
}

function fineNote(transaction) {
  const outstanding = Number(transaction.outstandingFine) || 0
  return outstanding > 0 ? 'unpaid' : 'paid'
}

// Table of borrow transactions. When `onChanged` is given, active loans get a
// Return action (the API restricts Members to their own transactions).
export function TransactionTable({ transactions, onChanged, showMember = false, onPreviewBook }) {
  const toast = useToast()
  const [pending, setPending] = useState(null)
  const [busy, setBusy] = useState(false)

  const handleReturn = useSingleFlight(async () => {
    setBusy(true)
    try {
      const result = await returnBook(pending.transactionId)
      const fine = Number(result?.transaction?.fineAmount) || 0
      toast.success(
        fine > 0
          ? `"${pending.book?.title ?? 'Book'}" returned. Late fine: ${formatCurrency(fine)}.`
          : `"${pending.book?.title ?? 'Book'}" returned.`,
      )
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
              <th>Due</th>
              <th>Returned</th>
              <th>Status</th>
              <th>Fine</th>
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
                <td data-label="Due">
                  {formatDueDate(transaction.dueDate)}
                  {dueNote(transaction) && (
                    <span className={`cell-sub${transaction.isOverdue ? ' cell-overdue' : ''}`}>
                      {dueNote(transaction)}
                    </span>
                  )}
                </td>
                <td data-label="Returned">{formatDate(transaction.returnDate)}</td>
                <td data-label="Status">
                  <StatusBadge status={transaction.displayStatus ?? transaction.status} />
                </td>
                <td data-label="Fine">
                  {transaction.currentFine > 0 ? (
                    <>
                      <span className={transaction.isOverdue ? 'cell-overdue' : undefined}>
                        {formatCurrency(transaction.currentFine)}
                      </span>
                      <span className="cell-sub">
                        {transaction.isOverdue ? 'so far' : fineNote(transaction)}
                      </span>
                    </>
                  ) : '—'}
                </td>
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
          message={
            `Mark "${pending.book?.title ?? 'this book'}" as returned. This will restore one copy to available inventory.` +
            (pending.isOverdue
              ? ` It is ${pending.overdueDays} day${pending.overdueDays === 1 ? '' : 's'} overdue, so a late fine of ${formatCurrency(pending.currentFine)} will be recorded.`
              : '')
          }
          confirmLabel="Return book"
          busy={busy}
          onConfirm={handleReturn}
          onCancel={() => setPending(null)}
        />
      )}
    </>
  )
}
