import { useCallback, useState } from 'react'
import { EmptyState, PageHeader, ResourceView } from '../../components/ui/PageParts.jsx'
import { useApiResource } from '../../hooks/useApiResource.js'
import { useSingleFlight } from '../../hooks/useSingleFlight.js'
import { useToast } from '../../hooks/useToast.js'
import { getApiErrorMessage } from '../../services/api.js'
import { getNotifications, runNotificationChecks } from '../../services/notificationService.js'

const TYPE_LABELS = {
  DueReminder: 'Due-date reminder',
  Overdue: 'Overdue notice',
  BookAvailable: 'Book available',
}

const STATUS_TONES = { Sent: 'active', Failed: 'danger', Pending: 'warning' }

// The API returns UTC timestamps without a zone suffix.
function formatDateTime(value) {
  if (!value) return '—'
  const hasZone = /(Z|[+-]\d\d:\d\d)$/.test(value)
  const date = new Date(hasZone ? value : `${value}Z`)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

// Admin page: were the notification emails sent or not?
export function Notifications() {
  const toast = useToast()
  const [type, setType] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [isRunning, setIsRunning] = useState(false)

  const fetchPage = useCallback(
    () => getNotifications({ type, status, page, pageSize: 25 }),
    [type, status, page],
  )
  const resource = useApiResource(fetchPage, 'Could not load notifications.')

  const handleRun = useSingleFlight(async () => {
    setIsRunning(true)
    try {
      const result = await runNotificationChecks()
      if (result.emailNotConfigured) {
        toast.error('Email is not configured on the server, so nothing was sent.')
      } else {
        const sent = result.dueReminders + result.overdueNotices + result.availabilityNotices
        toast.success(
          sent > 0
            ? `Checks finished: ${sent} email(s) sent${result.failed ? `, ${result.failed} failed` : ''}.`
            : result.failed
              ? `${result.failed} email(s) failed. See the list below.`
              : 'Checks finished: nothing new to send right now.',
        )
      }
      setPage(1)
      resource.reload()
    } catch (error) {
      toast.error(getApiErrorMessage(error, 'The checks could not be run.'))
    } finally {
      setIsRunning(false)
    }
  })

  const items = resource.data?.items ?? []
  const total = resource.data?.totalCount ?? 0
  const pageCount = Math.max(1, Math.ceil(total / 25))

  return (
    <>
      <PageHeader
        title="Notification emails"
        description="Every due-date reminder, overdue notice and book-available email, and whether it was delivered."
        actions={
          <button className="primary-button" type="button" onClick={handleRun} disabled={isRunning}>
            {isRunning ? 'Running...' : 'Run checks now'}
          </button>
        }
      />

      <div className="toolbar">
        <select
          value={type}
          onChange={(event) => { setType(event.target.value); setPage(1) }}
          aria-label="Filter by type"
        >
          <option value="">All types</option>
          {Object.entries(TYPE_LABELS).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <select
          value={status}
          onChange={(event) => { setStatus(event.target.value); setPage(1) }}
          aria-label="Filter by status"
        >
          <option value="">All results</option>
          <option value="Sent">Sent</option>
          <option value="Failed">Failed</option>
          <option value="Pending">Pending</option>
        </select>
        <button className="secondary-button" type="button" onClick={resource.reload}>Refresh</button>
      </div>

      <ResourceView
        resource={resource}
        isEmpty={items.length === 0}
        empty={
          <EmptyState
            title="No notification emails yet"
            message="Reminders appear here after the scheduler (or “Run checks now”) finds a loan that is due soon or overdue."
          />
        }
      >
        <div className="table-wrap">
          <table className="data-table" aria-label="Notification emails">
            <thead>
              <tr>
                <th>Type</th>
                <th>Recipient</th>
                <th>Book</th>
                <th>Result</th>
                <th>Sent at</th>
                <th>Attempts</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id}>
                  <td data-label="Type">{TYPE_LABELS[item.notificationType] ?? item.notificationType}</td>
                  <td data-label="Recipient">
                    <strong>{item.userName}</strong>
                    <span className="cell-sub">{item.userEmail}</span>
                  </td>
                  <td data-label="Book">{item.bookTitle}</td>
                  <td data-label="Result">
                    <span className={`badge badge-${STATUS_TONES[item.status] ?? 'neutral'}`}>{item.status}</span>
                    {item.errorMessage && <span className="cell-sub">{item.errorMessage}</span>}
                  </td>
                  <td data-label="Sent at">
                    {item.sentAt ? formatDateTime(item.sentAt) : (
                      <>—<span className="cell-sub">last tried {formatDateTime(item.lastAttemptAt)}</span></>
                    )}
                  </td>
                  <td data-label="Attempts">{item.attempts}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="toolbar" style={{ justifyContent: 'space-between', marginTop: 12 }}>
          <span className="muted-note">{total} notification{total === 1 ? '' : 's'}</span>
          <span>
            <button className="secondary-button small" type="button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Previous
            </button>{' '}
            <span className="muted-note">Page {page} of {pageCount}</span>{' '}
            <button className="secondary-button small" type="button" disabled={page >= pageCount} onClick={() => setPage(page + 1)}>
              Next
            </button>
          </span>
        </div>
      </ResourceView>
    </>
  )
}
