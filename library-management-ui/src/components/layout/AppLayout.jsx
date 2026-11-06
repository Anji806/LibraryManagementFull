import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { ConfirmDialog } from '../ui/Modal.jsx'
import { useAuth } from '../../hooks/useAuth.js'
import { handleMenuKeyDown } from '../../utils/keyboard.js'
import { PERMISSIONS, hasPermission } from '../../utils/roles.js'

// ── SVG icon set (inline, no external dependency) ──────────────────────────
function IconBook() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
    </svg>
  )
}

function IconGrid() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  )
}

function IconBooks() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
      <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
    </svg>
  )
}

function IconPen() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
    </svg>
  )
}

function IconTag() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
      <line x1="7" y1="7" x2="7.01" y2="7" />
    </svg>
  )
}

function IconArrowOut() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
      <polyline points="15 3 21 3 21 9" />
      <line x1="10" y1="14" x2="21" y2="3" />
    </svg>
  )
}

function IconArrowIn() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 13l-4-4 4-4" />
      <path d="M5 9h11a4 4 0 0 1 0 8h-1" />
    </svg>
  )
}

function IconClock() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  )
}

function IconUsers() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  )
}


function IconLogout() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <polyline points="16 17 21 12 16 7" />
      <line x1="21" y1="12" x2="9" y2="12" />
    </svg>
  )
}

function IconMenu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <line x1="3" y1="6" x2="21" y2="6" />
      <line x1="3" y1="12" x2="21" y2="12" />
      <line x1="3" y1="18" x2="21" y2="18" />
    </svg>
  )
}

function IconClose() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}

// Above this width the sidebar sits beside the content; at or below it, it becomes a drawer.
const DESKTOP_QUERY = '(min-width: 1025px)'

// ── Navigation items ─────────────────────────────────────────────────────────
const navigation = [
  { label: 'Overview', path: '/dashboard', icon: <IconGrid />, exact: true },
  { label: 'Book catalog', path: '/books', icon: <IconBooks />, permission: PERMISSIONS.READ_CATALOG, exact: true },
  { label: 'Manage books', path: '/books/manage', icon: <IconBook />, permission: PERMISSIONS.MANAGE_BOOKS },
  { label: 'Authors', path: '/authors', icon: <IconPen />, permission: PERMISSIONS.READ_CATALOG },
  { label: 'Categories', path: '/categories', icon: <IconTag />, permission: PERMISSIONS.READ_CATALOG },
  {
    label: 'Borrowing',
    memberLabel: 'My borrowed books',
    path: '/borrowing',
    icon: <IconArrowOut />,
    permission: PERMISSIONS.BORROW_BOOKS,
    exact: true,
  },
  {
    label: 'Returns',
    path: '/borrowing/returns',
    icon: <IconArrowIn />,
    permission: PERMISSIONS.LOOKUP_MEMBERS,
  },
  {
    label: 'Member lookup',
    memberLabel: 'My history',
    path: '/members',
    icon: <IconClock />,
    permission: PERMISSIONS.READ_MEMBER_HISTORY,
  },
  { label: 'Users', path: '/users', icon: <IconUsers />, permission: PERMISSIONS.MANAGE_USERS },
  { label: 'Login history', path: '/admin/login-history', icon: <IconClock />, permission: PERMISSIONS.READ_LOGIN_HISTORY },
]

// Page title derived from the current pathname
function usePageTitle() {
  const { pathname } = useLocation()
  const map = {
    '/dashboard': 'Overview',
    '/books': 'Book catalog',
    '/books/manage': 'Manage books',
    '/authors': 'Authors',
    '/categories': 'Categories',
    '/borrowing': 'Borrowing',
    '/borrowing/returns': 'Returns',
    '/members': 'Member lookup',
    '/users': 'Users',
    '/admin/login-history': 'Login history',
  }
  if (map[pathname]) return map[pathname]
  if (pathname.startsWith('/books/')) return 'Book details'
  if (pathname.startsWith('/users/')) return 'User details'
  return 'Library'
}

const SIDEBAR_STORAGE_KEY = 'library.sidebarCollapsed'

function readSidebarCollapsed() {
  try {
    return window.localStorage.getItem(SIDEBAR_STORAGE_KEY) === 'true'
  } catch {
    return false
  }
}

export function AppLayout() {
  const { user, role, logout } = useAuth()
  const navigate = useNavigate()
  const pageTitle = usePageTitle()

  // Desktop: the sidebar is a grid column that can be collapsed (preference persisted).
  // Tablet / mobile: the same sidebar is a slide-in drawer. One toggle drives both.
  const [isDesktop, setIsDesktop] = useState(() => window.matchMedia(DESKTOP_QUERY).matches)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(readSidebarCollapsed)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const sidebarVisible = isDesktop ? !sidebarCollapsed : drawerOpen
  const toggleRef = useRef(null)
  const drawerCloseRef = useRef(null)
  const mainRef = useRef(null)
  const [confirmingLogout, setConfirmingLogout] = useState(false)
  const lastPathname = useRef(null)
  const { pathname } = useLocation()

  // After client-side navigation, move focus to the page content (like a full page load would).
  useEffect(() => {
    // Skip the initial mount (also under StrictMode's double effect run).
    const previous = lastPathname.current
    lastPathname.current = pathname
    if (previous === null || previous === pathname) return
    mainRef.current?.focus({ preventScroll: true })
  }, [pathname])

  // Drawer: focus moves in on open; on close focus goes back to the toggle if it was inside.
  const drawerWasOpen = useRef(false)
  useEffect(() => {
    if (isDesktop) {
      drawerWasOpen.current = false
      return
    }
    if (drawerOpen) {
      drawerCloseRef.current?.focus()
    } else if (drawerWasOpen.current) {
      const focused = document.activeElement
      if (!focused || focused === document.body || focused.closest('#app-sidebar')) {
        toggleRef.current?.focus()
      }
    }
    drawerWasOpen.current = drawerOpen
  }, [isDesktop, drawerOpen])

  useEffect(() => {
    const query = window.matchMedia(DESKTOP_QUERY)
    function handleChange(event) {
      setIsDesktop(event.matches)
      setDrawerOpen(false)
    }
    query.addEventListener('change', handleChange)
    return () => query.removeEventListener('change', handleChange)
  }, [])

  useEffect(() => {
    if (isDesktop || !drawerOpen) return undefined
    function handleKey(event) {
      if (event.key === 'Escape') setDrawerOpen(false)
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', handleKey)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKey)
    }
  }, [isDesktop, drawerOpen])

  function toggleSidebar() {
    if (!isDesktop) {
      setDrawerOpen((current) => !current)
      return
    }
    setSidebarCollapsed((current) => {
      const next = !current
      try {
        window.localStorage.setItem(SIDEBAR_STORAGE_KEY, String(next))
      } catch {
        // Preference is optional; ignore storage failures.
      }
      return next
    })
  }

  // Esc inside the sidebar: close the drawer on small screens, otherwise hand focus to the page.
  function handleSidebarKeyDown(event) {
    if (event.key !== 'Escape' || event.defaultPrevented) return
    event.preventDefault()
    if (!isDesktop) setDrawerOpen(false)
    else mainRef.current?.focus()
  }

  function closeDrawer() {
    setDrawerOpen(false)
  }

  const initials = (user?.fullName || 'U')
    .split(/\s+/)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <>
    <a className="skip-link" href="#main-content" onClick={(event) => { event.preventDefault(); mainRef.current?.focus() }}>
      Skip to main content
    </a>
    <div className={`app-shell${isDesktop && sidebarCollapsed ? ' sidebar-collapsed' : ''}${!isDesktop && drawerOpen ? ' drawer-open' : ''}`}>
      {/* ── Sidebar ── */}
      <aside className="sidebar" id="app-sidebar" aria-label="Sidebar" inert={!sidebarVisible} onKeyDown={handleSidebarKeyDown}>
        <div className="sidebar-header">
          <div className="brand">
            <img className="brand-logo" src="/Library_icon.png" alt="" width="36" height="36" />
            <span>Greenleaf Library</span>
          </div>
          <button
            ref={drawerCloseRef}
            className="sidebar-close"
            type="button"
            onClick={closeDrawer}
            aria-label="Close navigation menu"
          >
            <IconClose />
          </button>
        </div>

        <div className="sidebar-nav-wrap">
          <p className="nav-section-label">Navigation</p>
          <nav className="side-nav" aria-label="Main navigation" onKeyDown={handleMenuKeyDown}>
            {navigation
              .filter((item) => !item.permission || hasPermission(role, item.permission))
              .map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.exact}
                  onClick={closeDrawer}
                >
                  <span className="nav-icon">{item.icon}</span>
                  {role === 'Member' && item.memberLabel ? item.memberLabel : item.label}
                </NavLink>
              ))}
          </nav>
        </div>

        <div className="sidebar-footer">
          <div className="sidebar-user">
            <span className="sidebar-avatar" aria-label={`${user?.fullName} profile`}>
              {initials}
            </span>
            <div className="sidebar-user-info">
              <div className="sidebar-user-name">{user?.fullName}</div>
              <div className="sidebar-user-role">{role}</div>
            </div>
            <button
              className="sidebar-logout"
              type="button"
              onClick={() => setConfirmingLogout(true)}
              aria-label="Sign out"
              title="Sign out"
            >
              <IconLogout />
            </button>
          </div>
        </div>
      </aside>

      {!isDesktop && drawerOpen && (
        <div className="sidebar-backdrop" onClick={closeDrawer} aria-hidden="true" />
      )}

      {/* ── Workspace ── */}
      <div className="workspace" inert={!isDesktop && drawerOpen}>
        <header className="topbar" onKeyDown={handleMenuKeyDown}>
          <div className="topbar-left">
            <button
              ref={toggleRef}
              className="sidebar-toggle"
              type="button"
              onClick={toggleSidebar}
              aria-controls="app-sidebar"
              aria-expanded={sidebarVisible}
              aria-label={isDesktop ? (sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar') : (drawerOpen ? 'Close navigation menu' : 'Open navigation menu')}
              title={isDesktop ? (sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar') : (drawerOpen ? 'Close navigation menu' : 'Open navigation menu')}
            >
              {!isDesktop && drawerOpen ? <IconClose /> : <IconMenu />}
            </button>
            <span className="topbar-title">{pageTitle}</span>
          </div>
          <div className="topbar-right">
            <div className="topbar-user">
              <div className="topbar-user-info">
                <span className="topbar-user-name">{user?.fullName}</span>
                <span className="topbar-user-email">{user?.email}</span>
              </div>
              <span className="topbar-avatar" aria-hidden="true">{initials}</span>
            </div>
            <button className="topbar-sign-out" type="button" onClick={() => setConfirmingLogout(true)}>
              Sign out
            </button>
          </div>
        </header>
        <main className="page-content" id="main-content" tabIndex={-1} ref={mainRef}>
          <Outlet />
        </main>
      </div>
    </div>
    {confirmingLogout && (
      <ConfirmDialog
        title="Sign out?"
        message="Are you sure you want to sign out?"
        confirmLabel="Yes"
        cancelLabel="No"
        onConfirm={handleLogout}
        onCancel={() => setConfirmingLogout(false)}
      />
    )}
    </>
  )
}
