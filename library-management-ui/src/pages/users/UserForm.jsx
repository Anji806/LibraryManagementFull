import { useState } from 'react'
import { Modal } from '../../components/ui/Modal.jsx'
import { FormError } from '../../components/ui/PageParts.jsx'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { createUser, updateUser } from '../../services/userService.js'
import { ALL_ROLES } from '../../utils/roles.js'

// Creates (register, then assign role) or edits a user.
export function UserForm({ user, onClose, onSaved }) {
  const toast = useToast()
  const editing = Boolean(user)
  const [form, setForm] = useState({
    fullName: user?.fullName ?? '',
    email: user?.email ?? '',
    password: '',
    role: user?.role ?? 'Member',
    isActive: user?.isActive ?? true,
    address: user?.address ?? '',
  })
  const [initialForm] = useState(form)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const dirty = JSON.stringify(form) !== JSON.stringify(initialForm)
  const handleSubmit = useSingleFlight(submit)

  function update(field) {
    return (event) => setForm((current) => ({ ...current, [field]: event.target.value }))
  }

  async function submit(event) {
    event.preventDefault()
    setError('')

    if (!form.fullName.trim()) return setError('Full name is required.')
    if (!editing && (!form.email.trim() || form.password.length < 6)) {
      return setError('Email and a password of at least 6 characters are required.')
    }

    setSaving(true)
    try {
      let userId = user?.userId
      if (!editing) {
        const created = await createUser({
          fullName: form.fullName.trim(),
          email: form.email.trim(),
          password: form.password,
        })
        userId = created.userId
      }

      const needsUpdate = editing || form.role !== 'Member' || form.address.trim()
      if (needsUpdate) {
        await updateUser(userId, {
          fullName: form.fullName.trim(),
          role: form.role,
          isActive: form.isActive,
          address: form.address.trim(),
        })
      }

      toast.success(editing ? 'User updated.' : 'User created.')
      onSaved()
    } catch (apiError) {
      setError(getApiErrorMessage(apiError, 'The user could not be saved.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={editing ? 'Edit user' : 'Create user'} onClose={onClose} busy={saving} dirty={dirty}>
      <form className="form-grid" onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="user-fullname">Full name <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <input id="user-fullname" value={form.fullName} onChange={update('fullName')} placeholder="Full name" required />
        </div>
        <div className="field">
          <label htmlFor="user-email">Email <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <input
            id="user-email"
            type="email"
            value={form.email}
            onChange={update('email')}
            placeholder="email@example.com"
            disabled={editing}
            required
          />
        </div>
        {!editing && (
          <div className="field">
            <label htmlFor="user-password">Password <span style={{ color: 'var(--color-error)' }}>*</span></label>
            <input
              id="user-password"
              type="password"
              minLength={6}
              value={form.password}
              onChange={update('password')}
              placeholder="At least 6 characters"
              required
            />
          </div>
        )}
        <div className="field">
          <label htmlFor="user-role">Role</label>
          <select id="user-role" value={form.role} onChange={update('role')}>
            {ALL_ROLES.map((role) => <option key={role} value={role}>{role}</option>)}
          </select>
        </div>
        <div className="field">
          <label htmlFor="user-address">Address</label>
          <input id="user-address" value={form.address} onChange={update('address')} placeholder="Optional address" />
        </div>
        {editing && (
          <label className="check-field">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))}
            />
            Account is active
          </label>
        )}
        <FormError message={error} />
        <div className="modal-actions">
          <button className="secondary-button" type="button" onClick={onClose} disabled={saving}>Cancel</button>
          <button className="primary-button" type="submit" disabled={saving}>
            {saving ? 'Saving...' : 'Save user'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
