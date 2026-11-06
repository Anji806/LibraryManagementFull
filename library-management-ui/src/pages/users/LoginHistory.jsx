import { useCallback, useEffect, useState } from 'react'
import { ErrorState, LoadingState, PageHeader } from '../../components/ui/PageParts.jsx'
import { useRef } from 'react'
import { getApiErrorMessage } from '../../services/api.js'
import { deleteLoginHistory, getAllLoginHistory, getLoginHistory } from '../../services/loginHistoryService.js'
import { ConfirmDialog, Modal } from '../../components/ui/Modal.jsx'
import { downloadLoginHistoryWorkbook, toLoginHistoryRows } from '../../utils/loginHistoryExcel.js'
import { runLoginActivityStream } from '../../services/loginActivityStream.js'

const emptyFilters = { username: '', status: '', fromUtc: '', toUtc: '' }

function toUtcBoundary(value, end = false) {
  if (!value) return undefined
  const date = new Date(`${value}T${end ? '23:59:59.999' : '00:00:00.000'}`)
  return date.toISOString()
}

function matchesCurrentFilters(item, applied) {
  if (applied.username && !item.username.toLowerCase().includes(applied.username.trim().toLowerCase())) return false
  if (applied.status && item.loginStatus !== applied.status) return false
  const timestamp = new Date(item.loginDateTime).getTime()
  const from = toUtcBoundary(applied.fromUtc)
  const to = toUtcBoundary(applied.toUtc, true)
  if (from && timestamp < new Date(from).getTime()) return false
  if (to && timestamp >= new Date(to).getTime()) return false
  return true
}

function addLoginRecord(result, item, view) {
  if (!result || !matchesCurrentFilters(item, view.applied)) return result
  if (result.items.some((row) => row.loginLogId === item.loginLogId)) {
    return { ...result, items: result.items.map((row) => row.loginLogId === item.loginLogId ? item : row) }
  }
  let items = result.items
  if (view.page === 1 && view.sortDirection === 'desc') {
    items = [...items, item].sort((left, right) => {
      const byDate = new Date(right.loginDateTime).getTime() - new Date(left.loginDateTime).getTime()
      return byDate || right.loginLogId - left.loginLogId
    }).slice(0, 25)
  }
  return { ...result, items, totalCount: result.totalCount + 1 }
}

function updateLogoutRecord(result, item) {
  if (!result) return result
  return { ...result, items: result.items.map((row) => row.loginLogId === item.loginLogId ? item : row) }
}

export function LoginHistory() {
  const [filters, setFilters] = useState(emptyFilters)
  const [applied, setApplied] = useState(emptyFilters)
  const [sortDirection, setSortDirection] = useState('desc')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [loading, setLoading] = useState(true)
  const [selectedIds, setSelectedIds] = useState([])
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false)
  const [removing, setRemoving] = useState(false)
  const [exportItems, setExportItems] = useState(null)
  const [exporting, setExporting] = useState(false)
  const [liveConnected, setLiveConnected] = useState(false)
  const hasLoadedRef = useRef(false)
  const seenLoginIdsRef = useRef(new Set())
  const pendingEventsRef = useRef(new Map())
  const viewRef = useRef({ applied, page, sortDirection })

  useEffect(() => {
    viewRef.current = { applied, page, sortDirection }
  }, [applied, page, sortDirection])

  const load = useCallback(async (silent = false) => {
    if (!silent) { setLoading(true); setError('') }
    try {
      const data = await getLoginHistory({
        username: applied.username || undefined,
        status: applied.status || undefined,
        fromUtc: toUtcBoundary(applied.fromUtc),
        toUtc: toUtcBoundary(applied.toUtc, true),
        page,
        pageSize: 25,
        sortDirection,
      })
      for (const item of data.items) seenLoginIdsRef.current.add(item.loginLogId)
      hasLoadedRef.current = true
      setResult(data)
      const pending = [...pendingEventsRef.current.values()]
      pendingEventsRef.current.clear()
      for (const event of pending) {
        if (event.name === 'login:new') {
          if (seenLoginIdsRef.current.has(event.record.loginLogId)) continue
          seenLoginIdsRef.current.add(event.record.loginLogId)
          setResult((current) => addLoginRecord(current, event.record, viewRef.current))
        } else {
          setResult((current) => updateLogoutRecord(current, event.record))
        }
      }
    } catch (requestError) {
      if (!silent) setError(getApiErrorMessage(requestError, 'Could not load login history.'))
    } finally {
      if (!silent) setLoading(false)
    }
  }, [applied, page, sortDirection])

  // Fetching is the synchronization side effect for the current filter/page state.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { load() }, [load])

  useEffect(() => {
    const controller = new AbortController()
    const onActivity = (name, record) => {
      if (!record || !Number.isSafeInteger(record.loginLogId)) return
      if (!hasLoadedRef.current) {
        const previous = pendingEventsRef.current.get(record.loginLogId)
        if (name === 'logout:update' && previous?.name === 'login:new')
          pendingEventsRef.current.set(record.loginLogId, { name: 'login:new', record })
        else pendingEventsRef.current.set(record.loginLogId, { name, record })
        return
      }
      if (name === 'login:new') {
        if (seenLoginIdsRef.current.has(record.loginLogId)) return
        seenLoginIdsRef.current.add(record.loginLogId)
        setResult((current) => addLoginRecord(current, record, viewRef.current))
      } else if (name === 'logout:update') {
        setResult((current) => updateLogoutRecord(current, record))
      }
    }

    runLoginActivityStream({
      onEvent: onActivity,
      onConnectionChange: setLiveConnected,
      signal: controller.signal,
    })
    return () => controller.abort()
  }, [])

  function applyFilters(event) {
    event.preventDefault()
    setSelectedIds([])
    setPage(1)
    setApplied({ ...filters })
  }

  const currentItems = result?.items ?? []
  const allVisibleSelected = currentItems.length > 0 && currentItems.every((item) => selectedIds.includes(item.loginLogId))
  const exportFilters = {
    username: applied.username || undefined,
    status: applied.status || undefined,
    fromUtc: toUtcBoundary(applied.fromUtc),
    toUtc: toUtcBoundary(applied.toUtc, true),
    sortDirection,
  }

  async function prepareExport() {
    setExporting(true)
    try {
      setExportItems(await getAllLoginHistory(exportFilters))
    } catch (requestError) {
      setError(getApiErrorMessage(requestError, 'Could not prepare the Excel export.'))
    } finally {
      setExporting(false)
    }
  }

  function downloadExport() {
    downloadLoginHistoryWorkbook(exportItems ?? [])
    setExportItems(null)
  }

  async function removeSelected() {
    setRemoving(true)
    setActionError('')
    try {
      const response = await deleteLoginHistory(selectedIds)
      if (response.deletedCount !== selectedIds.length) {
        setActionError('The server did not confirm removal of every selected record. Refresh the list and try again.')
        setRemoveDialogOpen(false)
        return
      }
      const deletedIds = new Set(selectedIds)
      const remainingCount = Math.max(0, (result?.totalCount ?? 0) - selectedIds.length)
      setResult((current) => current ? {
        ...current,
        items: current.items.filter((item) => !deletedIds.has(item.loginLogId)),
        totalCount: remainingCount,
      } : current)
      const nextPage = Math.min(page, Math.max(1, Math.ceil(remainingCount / 25)))
      setSelectedIds([])
      setRemoveDialogOpen(false)
      if (nextPage !== page) setPage(nextPage)
      else await load(true)
    } catch (requestError) {
      setActionError(getApiErrorMessage(requestError, 'Could not remove the selected history.'))
      setRemoveDialogOpen(false)
    } finally {
      setRemoving(false)
    }
  }

  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / 25))

  return (
    <section>
      <PageHeader title="Login history" description="Review recent sign-in activity and failed attempts." />
      <form className="login-history-filters" onSubmit={applyFilters}>
        <label>Username<input value={filters.username} onChange={(event) => setFilters({ ...filters, username: event.target.value })} /></label>
        <label>Status<select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}><option value="">All statuses</option><option value="Success">Success</option><option value="Failed">Failed</option></select></label>
        <label>From date<input type="date" value={filters.fromUtc} onChange={(event) => setFilters({ ...filters, fromUtc: event.target.value })} /></label>
        <label>To date<input type="date" value={filters.toUtc} onChange={(event) => setFilters({ ...filters, toUtc: event.target.value })} /></label>
        <button className="primary-button" type="submit">Apply filters</button>
      </form>
      {error ? <ErrorState message={error} onRetry={() => load()} /> : loading ? <LoadingState label="Loading login history..." /> : (
        <>
          <div className="login-history-toolbar">
            <div className="login-history-summary">{result?.totalCount ?? 0} records <span className={`login-history-live${liveConnected ? ' connected' : ''}`} role="status">{liveConnected ? 'Live updates on' : 'Connecting to live updates…'}</span></div>
            <div className="login-history-actions">
              <button className="danger-button" type="button" onClick={() => setRemoveDialogOpen(true)} disabled={!selectedIds.length || removing}>Remove selected{selectedIds.length ? ` (${selectedIds.length})` : ''}</button>
              <button className="secondary-button" type="button" onClick={prepareExport} disabled={exporting || !result?.totalCount}>{exporting ? 'Preparing…' : 'Download Excel'}</button>
            </div>
          </div>
          {actionError && <p className="login-history-action-error" role="alert">{actionError}</p>}
          <div className="table-wrap"><table className="data-table">
            <thead><tr><th><input aria-label="Select all records on this page" type="checkbox" checked={allVisibleSelected} onChange={(event) => setSelectedIds(event.target.checked ? [...new Set([...selectedIds, ...currentItems.map((item) => item.loginLogId)])] : selectedIds.filter((id) => !currentItems.some((item) => item.loginLogId === id)))} /></th><th><button className="table-sort-button" onClick={() => { setSelectedIds([]); setSortDirection((value) => value === 'desc' ? 'asc' : 'desc') }} type="button">Login date {sortDirection === 'desc' ? '↓' : '↑'}</button></th><th>Logout date</th><th>Username</th><th>Result</th><th>IP address</th><th>Browser</th><th>Operating system</th><th>Failure reason</th><th>Session status</th></tr></thead>
            <tbody>{currentItems.map((item) => <tr key={item.loginLogId}><td><input aria-label={`Select login record ${item.loginLogId}`} type="checkbox" checked={selectedIds.includes(item.loginLogId)} onChange={(event) => setSelectedIds((ids) => event.target.checked ? [...ids, item.loginLogId] : ids.filter((id) => id !== item.loginLogId))} /></td><td>{new Date(item.loginDateTime).toLocaleString('en-IN', { timeZone: 'Asia/Kolkata' })}</td><td>{item.logoutDateTime ? new Date(item.logoutDateTime).toLocaleString('en-IN', { timeZone: 'Asia/Kolkata' }) : '—'}</td><td>{item.username}</td><td><span className={`badge ${item.loginStatus === 'Success' ? 'badge-active' : 'badge-inactive'}`}>{item.loginStatus}</span></td><td>{item.ipAddress || '—'}</td><td>{item.browser || '—'}</td><td>{item.operatingSystem || '—'}</td><td>{item.failureReason || '—'}</td><td>{item.sessionStatus === 'Active' || item.sessionStatus === 'Expired' || item.sessionStatus === 'Logged Out' ? item.sessionStatus : '—'}</td></tr>)}</tbody>
          </table></div>
          {!result?.items?.length && <p className="login-history-empty">No login records match these filters.</p>}
          <div className="login-history-pagination"><span>Page {page} of {totalPages}</span><div><button className="secondary-button" type="button" onClick={() => { setSelectedIds([]); setPage((value) => Math.max(1, value - 1)) }} disabled={page <= 1}>Previous</button><button className="secondary-button" type="button" onClick={() => { setSelectedIds([]); setPage((value) => Math.min(totalPages, value + 1)) }} disabled={page >= totalPages}>Next</button></div></div>
        </>
      )}
      {removeDialogOpen && <ConfirmDialog title="Remove selected login history?" message={`This permanently removes ${selectedIds.length} selected login record${selectedIds.length === 1 ? '' : 's'}.`} confirmLabel="Remove history" busy={removing} onConfirm={removeSelected} onCancel={() => setRemoveDialogOpen(false)} />}
      {exportItems && <Modal title="Preview Excel export" onClose={() => setExportItems(null)}><p className="modal-text">{exportItems.length} records will be included in the Excel file.</p><div className="login-history-preview-wrap"><table className="data-table"><thead><tr>{['Login date', 'Logout date', 'Username', 'Result', 'IP address', 'Browser', 'Operating system', 'Failure reason', 'Session status'].map((heading) => <th key={heading}>{heading}</th>)}</tr></thead><tbody>{toLoginHistoryRows(exportItems.slice(0, 10)).map((row, index) => <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{cell}</td>)}</tr>)}</tbody></table></div>{exportItems.length > 10 && <p className="login-history-preview-note">Showing the first 10 records. All {exportItems.length} records will be downloaded.</p>}<div className="modal-actions"><button className="secondary-button" type="button" onClick={() => setExportItems(null)}>Cancel</button><button className="primary-button" type="button" onClick={downloadExport}>Download .xlsx</button></div></Modal>}
    </section>
  )
}
