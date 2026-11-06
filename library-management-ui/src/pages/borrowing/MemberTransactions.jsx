import { useCallback } from 'react'
import { EmptyState, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { getMemberBorrowHistory, getMemberBorrowedBooks } from '../../services/memberService.js'
import { TransactionTable } from './TransactionTable.jsx'

// Shows a member's current loans or full history. `onChanged` is only passed for loans.
export function MemberTransactions({ memberId, mode, showReturn = false }) {
  const fetcher = useCallback(
    () => (mode === 'history' ? getMemberBorrowHistory(memberId) : getMemberBorrowedBooks(memberId)),
    [memberId, mode],
  )
  const resource = useApiResource(fetcher, 'Could not load borrowing records.')

  return (
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
      />
    </ResourceView>
  )
}
