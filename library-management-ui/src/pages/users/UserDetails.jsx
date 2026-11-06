import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { getUserById } from '../../services/userService.js'
import { ROLES } from '../../utils/roles.js'
import { MemberTransactions } from '../borrowing/MemberTransactions.jsx'
import { UserForm } from './UserForm.jsx'

export function UserDetails() {
  const { id } = useParams()
  const fetchUser = useCallback(() => getUserById(id), [id])
  const resource = useApiResource(fetchUser, 'Could not load this user.')
  const [editing, setEditing] = useState(false)
  const user = resource.data

  return (
    <>
      <PageHeader
        title={user?.fullName ?? 'User details'}
        actions={
          <>
            <Link className="secondary-button" to="/users">← Back to users</Link>
            {user && (
              <button className="primary-button" type="button" onClick={() => setEditing(true)}>
                Edit user
              </button>
            )}
          </>
        }
      />
      <ResourceView resource={resource} isEmpty={!user}>
        {user && (
          <>
            <section className="panel">
              <dl className="detail-list">
                <div><dt>User ID</dt><dd>{user.userId}</dd></div>
                <div><dt>Email</dt><dd>{user.email}</dd></div>
                <div>
                  <dt>Role</dt>
                  <dd><span className="badge badge-role">{user.role}</span></dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>
                    <span className={`badge ${user.isActive ? 'badge-active' : 'badge-neutral'}`}>
                      {user.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </dd>
                </div>
                <div><dt>Address</dt><dd>{user.address || '—'}</dd></div>
              </dl>
            </section>
            {user.role === ROLES.MEMBER && (
              <>
                <h2 className="section-title">Borrowing history</h2>
                <MemberTransactions memberId={user.userId} mode="history" />
              </>
            )}
          </>
        )}
      </ResourceView>
      {editing && (
        <UserForm
          user={user}
          onClose={() => setEditing(false)}
          onSaved={() => {
            setEditing(false)
            resource.reload()
          }}
        />
      )}
    </>
  )
}
