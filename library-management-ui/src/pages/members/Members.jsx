import { useState } from 'react'
import { PageHeader } from '../../components/ui/PageParts.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { ROLES } from '../../utils/roles.js'
import { MemberTransactions } from '../borrowing/MemberTransactions.jsx'
import { MemberPicker } from './MemberPicker.jsx'

export function Members() {
  const { role, user } = useAuth()
  const [memberId, setMemberId] = useState(null)

  if (role === ROLES.MEMBER) {
    return (
      <>
        <PageHeader title="My borrowing history" description="Everything you have borrowed and returned." />
        <MemberTransactions memberId={user.userId} mode="history" />
      </>
    )
  }

  return (
    <>
      <PageHeader title="Member lookup" description="Find a member and review their full borrowing history." />
      <MemberPicker value={memberId} onSelect={setMemberId} />
      {memberId ? (
        <>
          <h2 className="section-title">Borrowing history</h2>
          <MemberTransactions key={memberId} memberId={memberId} mode="history" />
        </>
      ) : (
        <p className="muted-note">Select a member above to view their borrowing history.</p>
      )}
    </>
  )
}
