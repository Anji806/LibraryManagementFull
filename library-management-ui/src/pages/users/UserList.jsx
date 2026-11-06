import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ConfirmDialog } from '../../components/ui/Modal.jsx'
import { DeleteButton, EditButton } from '../../components/ui/RowActionButtons.jsx'
import { EmptyState, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { deleteUser, getUsers } from '../../services/userService.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'
import { UserForm } from './UserForm.jsx'

export function UserList() {
  const { user: currentUser } = useAuth()
  const toast = useToast()
  const users = useApiResource(getUsers, 'Could not load users.')
  const [editor, setEditor] = useState(null)
  const [toDelete, setToDelete] = useState(null)
  const [deleting, setDeleting] = useState(false)

  const handleDelete = useSingleFlight(async () => {
    setDeleting(true)
    try {
      await deleteUser(toDelete.userId)
      toast.success(`${toDelete.fullName} was deleted.`)
      users.reload()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The user could not be deleted.'))
    } finally {
      setDeleting(false)
      setToDelete(null)
    }
  })

  return (
    <>
      <PageHeader
        title="Users"
        description="Create and manage library user accounts and roles."
        actions={
          <button className="primary-button" type="button" onClick={() => setEditor({ user: null })}>
            Create user
          </button>
        }
      />
      <ResourceView
        resource={users}
        isEmpty={!users.data?.length}
        empty={<EmptyState title="No users found" />}
      >
        <div className="table-wrap">
          <table className="data-table" aria-label="Users" onKeyDown={handleTableKeyDown}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {users.data?.map((item) => (
                <tr key={item.userId}>
                  <td data-label="Name">
                    <Link to={`/users/${item.userId}`}><strong>{item.fullName}</strong></Link>
                  </td>
                  <td data-label="Email">
                    <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>{item.email}</span>
                  </td>
                  <td data-label="Role">
                    <span className="badge badge-role">{item.role}</span>
                  </td>
                  <td data-label="Status">
                    <span className={`badge ${item.isActive ? 'badge-active' : 'badge-neutral'}`}>
                      {item.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="row-actions">
                    <EditButton entity="user" name={item.fullName} onClick={() => setEditor({ user: item })} />
                    {item.userId !== currentUser?.userId && (
                      <DeleteButton entity="user" name={item.fullName} onClick={() => setToDelete(item)} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ResourceView>
      {editor && (
        <UserForm
          user={editor.user}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null)
            users.reload()
          }}
        />
      )}
      {toDelete && (
        <ConfirmDialog
          title="Delete this user?"
          message={`${toDelete.fullName} (${toDelete.email}) will be permanently removed. Users with borrowing records cannot be deleted.`}
          busy={deleting}
          onConfirm={handleDelete}
          onCancel={() => setToDelete(null)}
        />
      )}
    </>
  )
}
