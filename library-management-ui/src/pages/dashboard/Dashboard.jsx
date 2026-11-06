import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { LoadingState } from '../../components/ui/PageParts.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { getBooks } from '../../services/bookService.js'
import { getMemberBorrowedBooks, getMemberBorrowHistory } from '../../services/memberService.js'
import { getUsers } from '../../services/userService.js'
import { PERMISSIONS, ROLES, hasPermission, isStaff } from '../../utils/roles.js'

// ── SVG Icons ──────────────────────────────────────────────────────────────
function Icon({ children }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      width="18"
      height="18"
      aria-hidden="true"
    >
      {children}
    </svg>
  )
}

function IconCatalog() {
  return <Icon><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" /><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" /></Icon>
}

function IconManage() {
  return <Icon><rect x="2" y="3" width="20" height="14" rx="2" ry="2" /><line x1="8" y1="21" x2="16" y2="21" /><line x1="12" y1="17" x2="12" y2="21" /></Icon>
}

function IconLend() {
  return <Icon><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" /><polyline points="15 3 21 3 21 9" /><line x1="10" y1="14" x2="21" y2="3" /></Icon>
}


function IconHistory() {
  return <Icon><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></Icon>
}

function IconUsers() {
  return <Icon><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" /></Icon>
}

function IconAuthors() {
  return <Icon><path d="M12 20h9" /><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" /></Icon>
}

// ── Stat Card ──────────────────────────────────────────────────────────────
function StatCard({ label, value, tone = 'default' }) {
  return (
    <div className="stat-card">
      <p className="stat-label">{label}</p>
      <p className={`stat-value${tone === 'green' ? ' green' : tone === 'amber' ? ' amber' : ''}`}>
        {value}
      </p>
    </div>
  )
}

// ── Shortcut Card ──────────────────────────────────────────────────────────
function ShortcutCard({ icon, title, description, links }) {
  return (
    <div className="shortcut-card">
      <div className="shortcut-icon">{icon}</div>
      <div className="shortcut-body">
        <h3>{title}</h3>
        <p>{description}</p>
        <div className="shortcut-links">
          {links.map(({ label, to }) => (
            <Link key={to} className="secondary-button small" to={to}>
              {label}
            </Link>
          ))}
        </div>
      </div>
    </div>
  )
}

// ── Dashboard ──────────────────────────────────────────────────────────────
export function Dashboard() {
  const { user, role } = useAuth()
  const [loading, setLoading] = useState(true)
  const [metrics, setMetrics] = useState({
    totalTitles: 0,
    totalCopies: 0,
    availableCopies: 0,
    borrowedCopies: 0,
    totalUsers: 0,
    activeUsers: 0,
    myActiveBorrows: 0,
    myTotalHistory: 0,
  })
  const [error, setError] = useState('')

  useEffect(() => {
    let isMounted = true

    async function loadMetrics() {
      setLoading(true)
      setError('')

      try {
        const books = await getBooks()
        const totalTitles = books?.length ?? 0
        const totalCopies = books?.reduce((acc, b) => acc + (b.totalCopies || 0), 0) ?? 0
        const availableCopies = books?.reduce((acc, b) => acc + (b.availableCopies || 0), 0) ?? 0
        const borrowedCopies = Math.max(0, totalCopies - availableCopies)

        let totalUsers = 0
        let activeUsers = 0
        let myActiveBorrows = 0
        let myTotalHistory = 0

        if (role === ROLES.ADMIN) {
          try {
            const users = await getUsers()
            totalUsers = users?.length ?? 0
            activeUsers = users?.filter((u) => u.isActive)?.length ?? 0
          } catch {
            // Non-critical
          }
        } else if (role === ROLES.MEMBER && user?.userId) {
          try {
            const [borrowed, history] = await Promise.all([
              getMemberBorrowedBooks(user.userId),
              getMemberBorrowHistory(user.userId),
            ])
            myActiveBorrows = borrowed?.length ?? 0
            myTotalHistory = history?.length ?? 0
          } catch {
            // Non-critical
          }
        }

        if (isMounted) {
          setMetrics({ totalTitles, totalCopies, availableCopies, borrowedCopies, totalUsers, activeUsers, myActiveBorrows, myTotalHistory })
        }
      } catch {
        if (isMounted) setError('Could not load current library metrics.')
      } finally {
        if (isMounted) setLoading(false)
      }
    }

    loadMetrics()
    return () => { isMounted = false }
  }, [role, user?.userId])

  return (
    <div>
      {/* Welcome banner */}
      <div className="dashboard-welcome">
        <h2>Welcome back, {user?.fullName || 'Reader'}</h2>
        <p>Signed in as <strong>{role}</strong> · Greenleaf Library Management System</p>
      </div>

      {/* Metrics */}
      <h2 className="section-title">Library overview</h2>

      {loading ? (
        <LoadingState label="Loading metrics..." />
      ) : error ? (
        <p className="form-error" role="alert">{error}</p>
      ) : (
        <div className="stat-grid">
          {role === ROLES.ADMIN && (
            <>
              <StatCard label="Total titles" value={metrics.totalTitles} />
              <StatCard label="Available copies" value={metrics.availableCopies} tone="green" />
              <StatCard label="Borrowed copies" value={metrics.borrowedCopies} tone="amber" />
              <StatCard label="Registered users" value={metrics.totalUsers} />
              <StatCard label="Active users" value={metrics.activeUsers} tone="green" />
            </>
          )}
          {role === ROLES.LIBRARIAN && (
            <>
              <StatCard label="Catalog titles" value={metrics.totalTitles} />
              <StatCard label="Available copies" value={metrics.availableCopies} tone="green" />
              <StatCard label="Active borrows" value={metrics.borrowedCopies} tone="amber" />
            </>
          )}
          {role === ROLES.MEMBER && (
            <>
              <StatCard label="Catalog titles" value={metrics.totalTitles} />
              <StatCard label="My active borrows" value={metrics.myActiveBorrows} tone="amber" />
              <StatCard label="Total borrowed" value={metrics.myTotalHistory} />
            </>
          )}
        </div>
      )}

      {/* Shortcuts */}
      <h2 className="section-title">Quick actions</h2>
      <div className="shortcut-grid">
        <ShortcutCard
          icon={<IconCatalog />}
          title="Book catalog"
          description="Search and browse books held by the library, check copy availability."
          links={[{ label: 'Browse catalog', to: '/books' }]}
        />

        {role === ROLES.MEMBER && (
          <ShortcutCard
            icon={<IconLend />}
            title="My borrowed books"
            description="View your active borrowed titles and manage returns."
            links={[{ label: 'View loans', to: '/borrowing' }]}
          />
        )}

        {role === ROLES.MEMBER && (
          <ShortcutCard
            icon={<IconHistory />}
            title="My borrowing history"
            description="See everything you have borrowed and returned over time."
            links={[{ label: 'View history', to: '/members' }]}
          />
        )}

        {isStaff(role) && (
          <ShortcutCard
            icon={<IconManage />}
            title="Manage books"
            description="Add new titles, update metadata, and track copy counts."
            links={[{ label: 'Manage books', to: '/books/manage' }]}
          />
        )}

        {isStaff(role) && (
          <ShortcutCard
            icon={<IconLend />}
            title="Lending & returns"
            description="Issue books to members or process returned copies back into inventory."
            links={[
              { label: 'Lend books', to: '/borrowing' },
              { label: 'Process returns', to: '/borrowing/returns' },
            ]}
          />
        )}

        {isStaff(role) && (
          <ShortcutCard
            icon={<IconHistory />}
            title="Member lookup"
            description="Find library members and review their active loans and borrow histories."
            links={[{ label: 'Look up member', to: '/members' }]}
          />
        )}

        {hasPermission(role, PERMISSIONS.MANAGE_USERS) && (
          <ShortcutCard
            icon={<IconUsers />}
            title="User administration"
            description="Manage registered accounts, assign roles, and activate or deactivate accounts."
            links={[{ label: 'Manage users', to: '/users' }]}
          />
        )}

        {hasPermission(role, PERMISSIONS.MANAGE_AUTHORS) && (
          <ShortcutCard
            icon={<IconAuthors />}
            title="Authors & categories"
            description="Maintain catalog taxonomies — authors and book category classifications."
            links={[
              { label: 'Authors', to: '/authors' },
              { label: 'Categories', to: '/categories' },
            ]}
          />
        )}
      </div>
    </div>
  )
}
