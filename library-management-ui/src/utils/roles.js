export const ROLES = Object.freeze({
  ADMIN: 'Admin',
  LIBRARIAN: 'Librarian',
  MEMBER: 'Member',
})

export const ALL_ROLES = Object.freeze(Object.values(ROLES))

export const PERMISSIONS = Object.freeze({
  READ_CATALOG: 'readCatalog',
  MANAGE_BOOKS: 'manageBooks',
  DELETE_BOOKS: 'deleteBooks',
  MANAGE_AUTHORS: 'manageAuthors',
  MANAGE_CATEGORIES: 'manageCategories',
  BORROW_BOOKS: 'borrowBooks',
  RETURN_BOOKS: 'returnBooks',
  READ_MEMBER_HISTORY: 'readMemberHistory',
  LOOKUP_MEMBERS: 'lookupMembers',
  MANAGE_USERS: 'manageUsers',
  READ_LOGIN_HISTORY: 'readLoginHistory',
})

// Mirrors the [Authorize(Roles = ...)] attributes in the API controllers.
const permissionRoles = Object.freeze({
  [PERMISSIONS.READ_CATALOG]: ALL_ROLES,
  [PERMISSIONS.MANAGE_BOOKS]: [ROLES.ADMIN, ROLES.LIBRARIAN],
  [PERMISSIONS.DELETE_BOOKS]: [ROLES.ADMIN],
  [PERMISSIONS.MANAGE_AUTHORS]: [ROLES.ADMIN],
  [PERMISSIONS.MANAGE_CATEGORIES]: [ROLES.ADMIN],
  [PERMISSIONS.BORROW_BOOKS]: ALL_ROLES,
  [PERMISSIONS.RETURN_BOOKS]: ALL_ROLES,
  [PERMISSIONS.READ_MEMBER_HISTORY]: ALL_ROLES,
  [PERMISSIONS.LOOKUP_MEMBERS]: [ROLES.ADMIN, ROLES.LIBRARIAN],
  [PERMISSIONS.MANAGE_USERS]: [ROLES.ADMIN],
  [PERMISSIONS.READ_LOGIN_HISTORY]: [ROLES.ADMIN],
})

export function hasPermission(role, permission) {
  return permissionRoles[permission]?.includes(role) ?? false
}

export function isStaff(role) {
  return role === ROLES.ADMIN || role === ROLES.LIBRARIAN
}
