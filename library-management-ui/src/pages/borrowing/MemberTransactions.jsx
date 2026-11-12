import { useCallback, useState } from 'react'
import { BookCatalogDetails } from '../../components/books/BookCatalogDetails.jsx'
import { BookPreview } from '../../components/books/BookPreview.jsx'
import { Modal } from '../../components/ui/Modal.jsx'
import { EmptyState, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { getMemberBorrowHistory, getMemberBorrowedBooks } from '../../services/memberService.js'
import { TransactionTable } from './TransactionTable.jsx'

// The transaction carries the full book; BookPreview reads the flat catalog shape.
function toPreviewBook(book) {
  return {
    ...book,
    authorName: book.author?.authorName ?? book.authorName,
    categoryName: book.categoryNames ?? book.category?.categoryName ?? book.categoryName,
  }
}

// Shows a member's current loans or full history. `onChanged` is only passed for loans.
export function MemberTransactions({ memberId, mode, showReturn = false, previewable = false }) {
  const [detailsBook, setDetailsBook] = useState(null)
  const [previewBook, setPreviewBook] = useState(null)
  const fetcher = useCallback(
    () => (mode === 'history' ? getMemberBorrowHistory(memberId) : getMemberBorrowedBooks(memberId)),
    [memberId, mode],
  )
  const resource = useApiResource(fetcher, 'Could not load borrowing records.')

  return (
    <>
    <ResourceView
      resource={resource}
      isEmpty={!resource.data?.length}
      empty={
        <EmptyState
          title={mode === 'history' ? 'No borrowing history' : 'No books currently borrowed'}
        />
      }
    >
      <TransactionTable
        transactions={resource.data ?? []}
        onChanged={showReturn ? resource.reload : undefined}
        onPreviewBook={previewable ? (book) => setDetailsBook(toPreviewBook(book)) : undefined}
      />
    </ResourceView>
    {detailsBook && (
      <Modal title="Book details" onClose={() => setDetailsBook(null)}>
        <BookCatalogDetails
          book={detailsBook}
          canBorrow={false}
          onPreview={() => {
            setPreviewBook(detailsBook)
            setDetailsBook(null)
          }}
        />
      </Modal>
    )}
    {previewBook && (
      <Modal title="Book preview" onClose={() => setPreviewBook(null)}>
        <BookPreview key={previewBook.bookId} book={previewBook} />
      </Modal>
    )}
    </>
  )
}
