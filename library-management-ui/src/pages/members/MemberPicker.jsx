import { useState } from 'react'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { getUsers } from '../../services/userService.js'
import { ROLES } from '../../utils/roles.js'

function AdminMemberSelect({ value, onSelect }) {
  const users = useApiResource(getUsers, 'Could not load members.')
  const members = (users.data ?? []).filter((candidate) => candidate.role === ROLES.MEMBER)

  return (
    <div
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
        <label htmlFor="member-select">Select member</label>
        <select
          id="member-select"
          value={value ?? ''}
          disabled={users.loading}
          onChange={(event) => onSelect(event.target.value ? Number(event.target.value) : null)}
        >
          <option value="">{users.loading ? 'Loading members...' : 'Choose a member'}</option>
          {members.map((member) => (
            <option key={member.userId} value={member.userId}>
              {member.fullName} ({member.email}){member.isActive === false ? ' — inactive' : ''}
            </option>
          ))}
        </select>
      </div>
      {users.error && <p className="form-error" role="alert" style={{ width: '100%', margin: 0 }}>{users.error}</p>}
    </div>
  )
}

function IdMemberLookup({ onSelect }) {
  const [input, setInput] = useState('')

  function handleSubmit(event) {
    event.preventDefault()
    const id = Number(input)
    onSelect(Number.isInteger(id) && id > 0 ? id : null)
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
      <div className="field grow" style={{ margin: 0, minWidth: 'min(200px, 100%)' }}>
        <label htmlFor="member-id-input">Member ID</label>
        <input
          id="member-id-input"
          type="number"
          min="1"
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Enter a member ID"
        />
      </div>
      <button className="primary-button" type="submit" style={{ alignSelf: 'flex-end' }}>
        Look up
      </button>
    </form>
  )
}

// Admins pick from the user list; Librarians have no list endpoint, so they look up by ID.
export function MemberPicker({ value, onSelect }) {
  const { role } = useAuth()
  return role === ROLES.ADMIN
    ? <AdminMemberSelect value={value} onSelect={onSelect} />
    : <IdMemberLookup onSelect={onSelect} />
}
