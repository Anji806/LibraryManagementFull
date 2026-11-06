import { useState } from 'react'
import { DeleteButton, EditButton } from '../../components/ui/RowActionButtons.jsx'
import { ConfirmDialog, Modal } from '../../components/ui/Modal.jsx'
import { EmptyState, FormError, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { createAuthor, deleteAuthor, getAuthors, updateAuthor } from '../../services/authorService.js'
import { createCategory, deleteCategory, getCategories, updateCategory } from '../../services/categoryService.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'
import { PERMISSIONS, hasPermission } from '../../utils/roles.js'

const configs = {
  authors: {
    title: 'Authors',
    singular: 'author',
    description: 'Browse and manage the authors in the catalog.',
    idKey: 'authorId',
    nameKey: 'authorName',
    permission: PERMISSIONS.MANAGE_AUTHORS,
    list: getAuthors,
    create: createAuthor,
    update: updateAuthor,
    remove: deleteAuthor,
  },
  categories: {
    title: 'Categories',
    singular: 'category',
    description: 'Browse and manage book categories.',
    idKey: 'categoryId',
    nameKey: 'categoryName',
    permission: PERMISSIONS.MANAGE_CATEGORIES,
    list: getCategories,
    create: createCategory,
    update: updateCategory,
    remove: deleteCategory,
  },
}

function NameForm({ config, item, onClose, onSaved }) {
  const toast = useToast()
  const [name, setName] = useState(item?.[config.nameKey] ?? '')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const dirty = name !== (item?.[config.nameKey] ?? '')
  const handleSubmit = useSingleFlight(submit)

  async function submit(event) {
    event.preventDefault()
    if (!name.trim()) {
      setError('Name is required.')
      return
    }

    setSaving(true)
    try {
      const body = { [config.nameKey]: name.trim() }
      if (item) await config.update(item[config.idKey], body)
      else await config.create(body)
      toast.success(`${config.singular[0].toUpperCase()}${config.singular.slice(1)} saved.`)
      onSaved()
    } catch (apiError) {
      setError(getApiErrorMessage(apiError, `The ${config.singular} could not be saved.`))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={`${item ? 'Edit' : 'Add'} ${config.singular}`} onClose={onClose} busy={saving} dirty={dirty}>
      <form className="form-grid" onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor={`entity-name-input`}>Name <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <input
            id="entity-name-input"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder={`Enter ${config.singular} name`}
            required
            autoFocus
          />
        </div>
        <FormError message={error} />
        <div className="modal-actions">
          <button className="secondary-button" type="button" onClick={onClose} disabled={saving}>Cancel</button>
          <button className="primary-button" type="submit" disabled={saving}>
            {saving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// Lists authors or categories; the API only allows Admins to change them.
export function NamedEntityPage({ kind }) {
  const config = configs[kind]
  const { role } = useAuth()
  const toast = useToast()
  const canManage = hasPermission(role, config.permission)
  const resource = useApiResource(config.list, `Could not load ${kind}.`)
  const [editor, setEditor] = useState(null)
  const [toDelete, setToDelete] = useState(null)
  const [deleting, setDeleting] = useState(false)

  const handleDelete = useSingleFlight(async () => {
    setDeleting(true)
    try {
      await config.remove(toDelete[config.idKey])
      toast.success(`${toDelete[config.nameKey]} was deleted.`)
      resource.reload()
    } catch (error) {
      toast.error(
        getApiErrorMessage(error, `The ${config.singular} could not be deleted. It may still be used by books.`),
      )
    } finally {
      setDeleting(false)
      setToDelete(null)
    }
  })

  return (
    <>
      <PageHeader
        title={config.title}
        description={canManage ? `Add, rename, and remove ${kind} in the catalog.` : `Browse ${kind} in the catalog.`}
        actions={
          canManage && (
            <button className="primary-button" type="button" onClick={() => setEditor({ item: null })}>
              Add {config.singular}
            </button>
          )
        }
      />
      <ResourceView
        resource={resource}
        isEmpty={!resource.data?.length}
        empty={<EmptyState title={`No ${kind} yet`} message={canManage ? `Use "Add ${config.singular}" to create one.` : undefined} />}
      >
        <div className="table-wrap">
          <table className="data-table" aria-label={config.title} onKeyDown={handleTableKeyDown}>
            <thead>
              <tr>
                <th>Name</th>
                {canManage && <th aria-label="Actions" />}
              </tr>
            </thead>
            <tbody>
              {resource.data?.map((item) => (
                <tr key={item[config.idKey]}>
                  <td data-label="Name"><strong>{item[config.nameKey]}</strong></td>
                  {canManage && (
                    <td className="row-actions">
                      <EditButton entity={config.singular} name={item[config.nameKey]} onClick={() => setEditor({ item })} />
                      <DeleteButton entity={config.singular} name={item[config.nameKey]} onClick={() => setToDelete(item)} />
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ResourceView>
      {editor && (
        <NameForm
          config={config}
          item={editor.item}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null)
            resource.reload()
          }}
        />
      )}
      {toDelete && (
        <ConfirmDialog
          title={`Delete this ${config.singular}?`}
          message={`"${toDelete[config.nameKey]}" will be permanently removed.`}
          busy={deleting}
          onConfirm={handleDelete}
          onCancel={() => setToDelete(null)}
        />
      )}
    </>
  )
}
