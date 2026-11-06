import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from '../components/layout/AppLayout.jsx'
import { Login } from '../pages/auth/Login.jsx'
import { Register } from '../pages/auth/Register.jsx'
import { BookCatalog } from '../pages/books/BookCatalog.jsx'
import { BookDetails } from '../pages/books/BookDetails.jsx'
import { ManageBooks } from '../pages/books/ManageBooks.jsx'
import { Borrowing } from '../pages/borrowing/Borrowing.jsx'
import { Returns } from '../pages/borrowing/Returns.jsx'
import { NamedEntityPage } from '../pages/catalog/NamedEntityPage.jsx'
import { Dashboard } from '../pages/dashboard/Dashboard.jsx'
import { Members } from '../pages/members/Members.jsx'
import { Forbidden, NotFound } from '../pages/StatusPages.jsx'
import { UserDetails } from '../pages/users/UserDetails.jsx'
import { UserList } from '../pages/users/UserList.jsx'
import { LoginHistory } from '../pages/users/LoginHistory.jsx'
import { ALL_ROLES, PERMISSIONS, hasPermission } from '../utils/roles.js'
import { ProtectedRoute } from './ProtectedRoute.jsx'
import { RoleRoute } from './RoleRoute.jsx'

function PermissionRoute({ permission, children }) {
  const allowedRoles = ALL_ROLES.filter((candidate) =>
    hasPermission(candidate, permission),
  )

  return <RoleRoute allowedRoles={allowedRoles}>{children}</RoleRoute>
}

function guarded(permission, element) {
  return <PermissionRoute permission={permission}>{element}</PermissionRoute>
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/403" element={<Forbidden />} />
      <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/books" element={guarded(PERMISSIONS.READ_CATALOG, <BookCatalog />)} />
        <Route path="/books/manage" element={guarded(PERMISSIONS.MANAGE_BOOKS, <ManageBooks />)} />
        <Route path="/books/:id" element={guarded(PERMISSIONS.READ_CATALOG, <BookDetails />)} />
        <Route path="/authors" element={guarded(PERMISSIONS.READ_CATALOG, <NamedEntityPage kind="authors" />)} />
        <Route path="/categories" element={guarded(PERMISSIONS.READ_CATALOG, <NamedEntityPage kind="categories" />)} />
        <Route path="/borrowing" element={guarded(PERMISSIONS.BORROW_BOOKS, <Borrowing />)} />
        <Route path="/borrowing/returns" element={guarded(PERMISSIONS.RETURN_BOOKS, <Returns />)} />
        <Route path="/members" element={guarded(PERMISSIONS.READ_MEMBER_HISTORY, <Members />)} />
        <Route path="/users" element={guarded(PERMISSIONS.MANAGE_USERS, <UserList />)} />
        <Route path="/admin/login-history" element={guarded(PERMISSIONS.READ_LOGIN_HISTORY, <LoginHistory />)} />
        <Route path="/users/:id" element={guarded(PERMISSIONS.MANAGE_USERS, <UserDetails />)} />
        {/* Authors/categories are managed in place by Admins on their list pages. */}
        <Route path="/authors/manage" element={<Navigate to="/authors" replace />} />
        <Route path="/categories/manage" element={<Navigate to="/categories" replace />} />
      </Route>
      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}
