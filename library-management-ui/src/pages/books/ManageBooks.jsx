import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { DeleteButton, EditButton } from '../../components/ui/RowActionButtons.jsx'
import { ConfirmDialog, Modal } from '../../components/ui/Modal.jsx'
import { EmptyState, FormError, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useAuth } from '../../hooks/useAuth.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getAuthors } from '../../services/authorService.js'
import { createBook, deleteBook, getBooks, updateBook } from '../../services/bookService.js'
import { getCategories } from '../../services/categoryService.js'
import { filterBooks } from '../../utils/books.js'
import { handleTableKeyDown } from '../../utils/keyboard.js'
import { PERMISSIONS, hasPermission } from '../../utils/roles.js'

const emptyForm = {
  title: '',
  isbn: '',
  authorId: '',
  categoryId: '',
  totalCopies: 1,
  publishedYear: new Date().getFullYear(),
  previewIntroduction: '',
  previewTheme: '',
  previewAbstract: '',
  previewMotto: '',
  previewWhyRead: '',
  coverUrl: '',
}

function BookForm({ book, authors, categories, onClose, onSaved }) {
  const toast = useToast()
  const editing = Boolean(book)
  const [form, setForm] = useState(
    editing
      ? {
          title: book.title,
          isbn: book.isbn,
          authorId: book.authorId,
          categoryId: book.categoryId,
          totalCopies: book.totalCopies,
          publishedYear: book.publishedYear,
          previewIntroduction: book.previewIntroduction ?? '',
          previewTheme: book.previewTheme ?? '',
          previewAbstract: book.previewAbstract ?? '',
          previewMotto: book.previewMotto ?? '',
          previewWhyRead: book.previewWhyRead ?? '',
          coverUrl: book.coverUrl ?? '',
        }
      : emptyForm,
  )
  const [initialForm] = useState(form)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const dirty = JSON.stringify(form) !== JSON.stringify(initialForm)

  function update(field) {
    return (event) => setForm((current) => ({ ...current, [field]: event.target.value }))
  }

  const handleSubmit = useSingleFlight(submit)

  async function submit(event) {
    event.preventDefault()
    setError('')

    if (!form.title.trim() || !form.authorId || !form.categoryId || (!editing && !form.isbn.trim())) {
      setError('Please complete all required fields.')
      return
    }

    const payload = {
      title: form.title.trim(),
      authorId: Number(form.authorId),
      categoryId: Number(form.categoryId),
      totalCopies: Number(form.totalCopies),
      publishedYear: Number(form.publishedYear),
      previewIntroduction: form.previewIntroduction.trim() || null,
      previewTheme: form.previewTheme.trim() || null,
      previewAbstract: form.previewAbstract.trim() || null,
      previewMotto: form.previewMotto.trim() || null,
      previewWhyRead: form.previewWhyRead.trim() || null,
      coverUrl: form.coverUrl.trim() || null,
    }

    setSaving(true)
    try {
      if (editing) {
        await updateBook(book.bookId, payload)
        toast.success('Book updated.')
      } else {
        await createBook({ ...payload, isbn: form.isbn.trim() })
        toast.success('Book added.')
      }
      onSaved()
    } catch (apiError) {
      setError(getApiErrorMessage(apiError, 'The book could not be saved.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={editing ? 'Edit book' : 'Add book'} onClose={onClose} busy={saving} dirty={dirty}>
      <form className="form-grid" onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="book-title">Title <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <input
            id="book-title"
            value={form.title}
            onChange={update('title')}
            placeholder="Enter book title"
            required
          />
        </div>
        {!editing && (
          <div className="field">
            <label htmlFor="book-isbn">ISBN <span style={{ color: 'var(--color-error)' }}>*</span></label>
            <input
              id="book-isbn"
              value={form.isbn}
              onChange={update('isbn')}
              placeholder="e.g. 978-0-06-112008-4"
              required
            />
          </div>
        )}
        <div className="field">
          <label htmlFor="book-author">Author <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <select id="book-author" value={form.authorId} onChange={update('authorId')} required>
            <option value="">Select an author</option>
            {authors.map((author) => (
              <option key={author.authorId} value={author.authorId}>{author.authorName}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="book-category">Category <span style={{ color: 'var(--color-error)' }}>*</span></label>
          <select id="book-category" value={form.categoryId} onChange={update('categoryId')} required>
            <option value="">Select a category</option>
            {categories.map((category) => (
              <option key={category.categoryId} value={category.categoryId}>{category.categoryName}</option>
            ))}
          </select>
        </div>
        <div className="form-row">
          <div className="field">
            <label htmlFor="book-copies">Total copies</label>
            <input
              id="book-copies"
              type="number"
              min="1"
              value={form.totalCopies}
              onChange={update('totalCopies')}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="book-year">Published year</label>
            <input
              id="book-year"
              type="number"
              min="1"
              max="9999"
              value={form.publishedYear}
              onChange={update('publishedYear')}
              required
            />
          </div>
        </div>
        <details className="book-preview-fields">
          <summary>Preview content and cover</summary>
          <p>Add verified book-specific copy here. Empty fields won’t be replaced with generated summaries.</p>
          <div className="field">
            <label htmlFor="book-preview-introduction">Book introduction</label>
            <textarea id="book-preview-introduction" value={form.previewIntroduction} onChange={update('previewIntroduction')} rows="4" maxLength="12000" />
          </div>
          <div className="field">
            <label htmlFor="book-preview-theme">Main theme</label>
            <textarea id="book-preview-theme" value={form.previewTheme} onChange={update('previewTheme')} rows="3" maxLength="12000" />
          </div>
          <div className="field">
            <label htmlFor="book-preview-abstract">Abstract and story overview</label>
            <textarea id="book-preview-abstract" value={form.previewAbstract} onChange={update('previewAbstract')} rows="5" maxLength="12000" />
          </div>
          <div className="field">
            <label htmlFor="book-preview-motto">Motto</label>
            <textarea id="book-preview-motto" value={form.previewMotto} onChange={update('previewMotto')} rows="2" maxLength="1000" />
          </div>
          <div className="field">
            <label htmlFor="book-preview-why">Why read this book?</label>
            <textarea id="book-preview-why" value={form.previewWhyRead} onChange={update('previewWhyRead')} rows="3" maxLength="3000" />
          </div>
          <div className="field">
            <label htmlFor="book-cover-url">Cover image URL</label>
            <input id="book-cover-url" type="url" value={form.coverUrl} onChange={update('coverUrl')} placeholder="https://..." />
          </div>
        </details>
        <FormError message={error} />
        <div className="modal-actions">
          <button className="secondary-button" type="button" onClick={onClose} disabled={saving}>Cancel</button>
          <button className="primary-button" type="submit" disabled={saving}>
            {saving ? 'Saving...' : 'Save book'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

const fetchLookups = () => Promise.all([getAuthors(), getCategories()])

export function ManageBooks() {
  const { role } = useAuth()
  const toast = useToast()
  const [input, setInput] = useState('')
  const [query, setQuery] = useState('')
  const [editor, setEditor] = useState(null)
  const [toDelete, setToDelete] = useState(null)
  const [deleting, setDeleting] = useState(false)

  const fetchBooks = useCallback(async () => filterBooks(await getBooks(), query), [query])
  const books = useApiResource(fetchBooks, 'Could not load books.')
  const lookups = useApiResource(fetchLookups, 'Could not load authors and categories.')
  const [authors, categories] = lookups.data ?? [[], []]
  const canDelete = hasPermission(role, PERMISSIONS.DELETE_BOOKS)

  const handleDelete = useSingleFlight(async () => {
    setDeleting(true)
    try {
      await deleteBook(toDelete.bookId)
      toast.success(`"${toDelete.title}" was deleted.`)
      books.reload()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The book could not be deleted.'))
    } finally {
      setDeleting(false)
      setToDelete(null)
    }
  })

  return (
    <>
      <PageHeader
        title="Manage books"
        description="Add, edit, and remove books in the catalog."
        actions={
          <button
            className="primary-button"
            type="button"
            disabled={lookups.loading || Boolean(lookups.error)}
            onClick={() => setEditor({ book: null })}
          >
            Add book
          </button>
        }
      />
      {lookups.error && <p className="form-error" role="alert">{lookups.error}</p>}
      <form
        className="toolbar"
        role="search"
        onSubmit={(event) => {
          event.preventDefault()
          setQuery(input.trim())
        }}
      >
        <input
          type="search"
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Search by title, author, category or year..."
          aria-label="Search books by title, author, category or year"
        />
        <button className="primary-button" type="submit">Search</button>
        {query && (
          <button className="secondary-button" type="button" onClick={() => { setInput(''); setQuery('') }}>
            Clear
          </button>
        )}
      </form>
      <ResourceView
        resource={books}
        isEmpty={!books.data?.length}
        empty={<EmptyState title={query ? 'No matching books' : 'No books yet'} message='Use "Add book" to create one.' />}
      >
        <div className="table-wrap">
          <table className="data-table" aria-label="Books" onKeyDown={handleTableKeyDown}>
            <thead>
              <tr>
                <th>Title</th>
                <th>Author</th>
                <th>Category</th>
                <th>ISBN</th>
                <th>Year</th>
                <th>Copies</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {books.data?.map((book) => (
                <tr key={book.bookId}>
                  <td data-label="Title">
                    <Link to={`/books/${book.bookId}`}><strong>{book.title}</strong></Link>
                  </td>
                  <td data-label="Author">{book.authorName}</td>
                  <td data-label="Category">{book.categoryName}</td>
                  <td data-label="ISBN">
                    <span style={{ fontFamily: 'monospace', fontSize: '12.5px', color: 'var(--text-muted)' }}>
                      {book.isbn}
                    </span>
                  </td>
                  <td data-label="Year">{book.publishedYear}</td>
                  <td data-label="Copies">
                    <span className={`badge ${book.availableCopies > 0 ? 'badge-active' : 'badge-neutral'}`}>
                      {book.availableCopies}/{book.totalCopies}
                    </span>
                  </td>
                  <td className="row-actions">
                    <EditButton entity="book" name={book.title} onClick={() => setEditor({ book })} />
                    {canDelete && (
                      <DeleteButton entity="book" name={book.title} onClick={() => setToDelete(book)} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ResourceView>
      {editor && (
        <BookForm
          book={editor.book}
          authors={authors}
          categories={categories}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null)
            books.reload()
          }}
        />
      )}
      {toDelete && (
        <ConfirmDialog
          title="Delete this book?"
          message={`"${toDelete.title}" will be permanently removed. This cannot be undone.`}
          busy={deleting}
          onConfirm={handleDelete}
          onCancel={() => setToDelete(null)}
        />
      )}
    </>
  )
}
