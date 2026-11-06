# GitHub Copilot Instructions — Library Management System

## 1. Project Overview

This repository contains a Library Management System with two completely separate applications:

1. **Backend** — ASP.NET Core Web API
2. **Frontend** — React application

The frontend must consume the backend through HTTP APIs. The frontend must never directly access SQL Server, Entity Framework Core, repositories, or backend C# classes.

The assignment requires:

- ASP.NET Core Web API
- .NET 10
- Entity Framework Core
- SQL Server
- Repository Pattern
- JWT Authentication
- Role-Based Authorization
- LINQ
- Dependency Injection
- DTOs
- Swagger/OpenAPI
- Async/Await

The frontend must use:

- React
- Vite
- JavaScript/JSX
- React Router
- Axios

Do not introduce unrelated frameworks or technologies unless explicitly requested.

---

## 2. Repository Structure

The expected repository structure is:

```text
D:\Anji\LibraryManagement\
│
├── .github\
│   └── copilot-instructions.md
│
├── LibraryManagement\              # BACKEND ONLY
│   ├── Controllers\
│   ├── Models\
│   ├── DTOs\
│   ├── Data\
│   ├── Repositories\
│   ├── Services\
│   ├── Middleware\
│   ├── Migrations\
│   ├── Program.cs
│   ├── appsettings.json
│   └── LibraryManagement.csproj
│
├── library-management-ui\          # FRONTEND ONLY
│   ├── public\
│   ├── src\
│   ├── package.json
│   └── vite.config.js
│
└── LibraryManagement.sln
```

### Strict separation rule

**Backend folder:**

```text
LibraryManagement/
```

contains only ASP.NET Core/C# backend code.

**Frontend folder:**

```text
library-management-ui/
```

contains only React/JavaScript frontend code.

Never:

- Create React files inside `LibraryManagement/`.
- Create C# controllers/models/repositories inside `library-management-ui/`.
- Access SQL Server directly from React.
- Import backend C# files into React.
- Duplicate backend business logic in React.
- Put EF Core logic in the frontend.

The frontend communicates with the backend only through HTTP APIs.

---

# 3. Backend Is the Source of Truth

The existing ASP.NET Core backend is the authoritative definition of:

- API routes
- HTTP methods
- Request DTOs
- Response DTOs
- JWT claims
- Roles
- Authorization
- Validation rules
- Business rules
- HTTP status codes

Before implementing a frontend API integration:

1. Inspect the backend controller.
2. Inspect its request/response DTOs.
3. Inspect authorization attributes.
4. Inspect Swagger/OpenAPI when available.
5. Use the actual route and payload exposed by the backend.

Do not invent endpoint names when the backend already provides them.

If the assignment and implementation differ, prefer the actual implemented backend contract and make the difference explicit in code/comments when necessary.

---

# 4. React Frontend Architecture

Use a clean, maintainable React structure:

```text
library-management-ui/
├── public/
├── src/
│   ├── assets/
│   │
│   ├── components/
│   │   ├── common/
│   │   ├── layout/
│   │   ├── books/
│   │   ├── authors/
│   │   ├── categories/
│   │   ├── users/
│   │   └── borrowing/
│   │
│   ├── pages/
│   │   ├── auth/
│   │   │   ├── Login.jsx
│   │   │   └── Register.jsx
│   │   ├── dashboard/
│   │   │   └── Dashboard.jsx
│   │   ├── books/
│   │   ├── authors/
│   │   ├── categories/
│   │   ├── users/
│   │   ├── borrowing/
│   │   └── member/
│   │
│   ├── routes/
│   │   ├── AppRoutes.jsx
│   │   ├── ProtectedRoute.jsx
│   │   └── RoleRoute.jsx
│   │
│   ├── services/
│   │   ├── api.js
│   │   ├── authService.js
│   │   ├── bookService.js
│   │   ├── authorService.js
│   │   ├── categoryService.js
│   │   ├── userService.js
│   │   └── borrowingService.js
│   │
│   ├── context/
│   │   └── AuthContext.jsx
│   │
│   ├── hooks/
│   │   └── useAuth.js
│   │
│   ├── utils/
│   │   ├── auth.js
│   │   ├── roles.js
│   │   └── constants.js
│   │
│   ├── App.jsx
│   └── main.jsx
│
├── .env
├── .env.example
├── package.json
└── vite.config.js
```

Keep the architecture simple. Do not create abstractions merely for the sake of abstraction.

---

# 5. API Configuration

The backend base URL must be configurable through an environment variable.

Example:

```env
VITE_API_BASE_URL=https://localhost:xxxx/api
```

Do not hard-code the backend URL throughout the application.

Create a centralized Axios client:

```text
src/services/api.js
```

All API services must use this client.

Example conceptual structure:

```javascript
import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

export default api;
```

The exact implementation may be adjusted to match the backend.

Never place secrets in frontend environment variables. Anything exposed through a Vite frontend build must be considered public.

---

# 6. Authentication

Required backend authentication endpoints:

```text
POST /api/auth/register
POST /api/auth/login
```

Registration requires:

- Full Name
- Email
- Password

Login requires:

- Email
- Password

The backend generates a JWT after successful login.

The JWT contains claims such as:

- UserId
- Email
- Role

The frontend must:

1. Submit credentials.
2. Receive the authentication response.
3. Store the authentication session appropriately.
4. Determine the user's role.
5. Update AuthContext.
6. Redirect to the appropriate dashboard.
7. Include the JWT on protected requests.
8. Support logout.
9. Clear authentication state during logout.
10. Handle invalid/expired authentication.

Never store or log plaintext passwords.

Never print JWT tokens to the browser console.

---

# 7. Authentication State

Create:

```text
src/context/AuthContext.jsx
```

Expose a simple authentication API such as:

```javascript
{
  user,
  token,
  role,
  isAuthenticated,
  isLoading,
  login,
  logout
}
```

Create:

```text
src/hooks/useAuth.js
```

Components should use:

```javascript
const { user, role, isAuthenticated } = useAuth();
```

Do not duplicate authentication state in individual pages.

During application startup, restore the authentication state if a valid session exists.

Do not render protected application content while authentication initialization is still being determined.

---

# 8. JWT Request Handling

Configure the Axios client with a request interceptor.

Every protected API request must send:

```http
Authorization: Bearer <JWT>
```

Do not manually repeat token handling in every service.

The interceptor should obtain the current token from the application's authentication/session mechanism.

Avoid circular dependencies between `api.js` and `AuthContext.jsx`.

Keep token handling centralized.

---

# 9. HTTP Error Handling

Handle the following statuses correctly:

```text
200 OK
201 Created
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
500 Internal Server Error
```

These are required by the assignment.

## 401 Unauthorized

A 401 means the user is not authenticated or the authentication token is invalid/expired.

The frontend should:

- Clear invalid authentication when appropriate.
- Redirect the user to login when appropriate.
- Display a useful message.

Do not expose technical exception details to the user.

## 403 Forbidden

A 403 means:

- The user is authenticated.
- The user does not have permission to perform the requested operation.

Do not automatically log the user out.

Display an appropriate unauthorized/forbidden message or route the user to a 403 page.

---

# 10. Roles

The application has exactly three required roles:

```text
Admin
Librarian
Member
```

Centralize role values:

```javascript
export const ROLES = {
  ADMIN: "Admin",
  LIBRARIAN: "Librarian",
  MEMBER: "Member",
};
```

Do not scatter string literals such as `"Admin"` throughout many components.

If the backend uses a different exact claim value, use the backend's actual value consistently.

---

# 11. Role Permissions

## Admin

Admin can:

- Manage users.
- Add books.
- View books.
- Update books.
- Delete books.
- Add authors.
- View authors.
- Update authors.
- Delete authors.
- Add categories.
- View categories.
- Update categories.
- Delete categories.
- View all borrowing transactions.

## Librarian

Librarian can:

- View books.
- Add books.
- Update books.
- View members.
- Issue books.
- Accept returned books.
- View borrowing history.

Librarian cannot delete books.

Librarian cannot manage users, authors, or categories as Admin.

## Member

Member can:

- View books.
- Search books.
- View their own borrowed books.
- View their own borrowing history.

Member cannot:

- Add books.
- Update books.
- Delete books.
- Manage authors.
- Manage categories.
- Manage users.
- Issue books.
- Return books on behalf of the library.

---

# 12. Permission Matrix

Use this matrix when deciding what should appear in the frontend.

| Feature | Admin | Librarian | Member |
|---|---:|---:|---:|
| Dashboard | Yes | Yes | Yes |
| View Books | Yes | Yes | Yes |
| Search Books | Yes | Yes | Yes |
| Add Book | Yes | Yes | No |
| Update Book | Yes | Yes | No |
| Delete Book | Yes | No | No |
| View Authors | Yes | No | No |
| Add Author | Yes | No | No |
| Update Author | Yes | No | No |
| Delete Author | Yes | No | No |
| View Categories | Yes | No | No |
| Add Category | Yes | No | No |
| Update Category | Yes | No | No |
| Delete Category | Yes | No | No |
| Manage Users | Yes | No | No |
| View Members | Yes | Yes | No |
| Issue Book | No | Yes | No |
| Return Book | No | Yes | No |
| View All Borrowing | Yes | Yes | No |
| My Borrowed Books | No | No | Yes |
| My Borrowing History | No | No | Yes |

The backend remains responsible for enforcing these permissions.

---

# 13. Protected Routes

Create:

```text
src/routes/ProtectedRoute.jsx
src/routes/RoleRoute.jsx
```

`ProtectedRoute`:

- Allows authenticated users.
- Redirects unauthenticated users to `/login`.

`RoleRoute`:

- Checks authentication.
- Checks the user's role.
- Allows only the configured roles.
- Redirects unauthorized authenticated users to a forbidden page.

Example:

```jsx
<RoleRoute allowedRoles={[ROLES.ADMIN]}>
  <AdminPage />
</RoleRoute>
```

Example:

```jsx
<RoleRoute allowedRoles={[ROLES.ADMIN, ROLES.LIBRARIAN]}>
  <BookManagement />
</RoleRoute>
```

Never rely only on hiding navigation links.

A user can manually enter a URL, so route protection is required.

The backend must independently enforce authorization.

---

# 14. Navigation

Create a shared application layout with:

- Header
- Sidebar/navigation
- Main content area
- User information
- Logout action

Navigation must be role-aware.

Do not show Admin-only navigation to Members or Librarians.

Do not show Librarian-only operations to Members.

Do not assume that hiding a menu item provides security.

Navigation is for user experience; backend authorization is for security.

---

# 15. Required Pages

Implement pages progressively.

## Authentication

```text
/login
/register
```

## Common

```text
/dashboard
/books
```

## Admin

```text
/users
/authors
/categories
/books/new
/books/:id/edit
/borrowing
```

## Librarian

```text
/books
/members
/borrowing/issue
/borrowing/return
/borrowing/history
```

## Member

```text
/books
/my-books
/my-history
```

Add a dedicated forbidden page:

```text
/403
```

Add a not-found page:

```text
/*
```

Routes should be adjusted to match the actual implemented application.

---

# 16. Backend API Endpoints

The assignment defines these endpoints.

## Authentication

```text
POST /api/auth/register
POST /api/auth/login
```

## Books

```text
GET    /api/books
GET    /api/books/{id}
POST   /api/books
PUT    /api/books/{id}
DELETE /api/books/{id}
```

## Authors

```text
GET    /api/authors
GET    /api/authors/{id}
POST   /api/authors
PUT    /api/authors/{id}
DELETE /api/authors/{id}
```

## Categories

```text
GET    /api/categories
GET    /api/categories/{id}
POST   /api/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}
```

## Borrowing

```text
POST /api/borrowtransactions/borrow
PUT  /api/borrowtransactions/return/{transactionId}
GET  /api/members/{memberId}/borrowed-books
GET  /api/books/{bookId}/borrow-history
```

## Search

```text
GET /api/books/search?title=dotnet
```

The exact implemented backend routes and DTO shapes take precedence over these assignment examples.

---

# 17. API Service Layer

Create one service module per major API area:

```text
authService.js
bookService.js
authorService.js
categoryService.js
userService.js
borrowingService.js
```

Example:

```javascript
export const getBooks = () => api.get("/books");

export const getBookById = (id) => api.get(`/books/${id}`);

export const createBook = (book) => api.post("/books", book);

export const updateBook = (id, book) =>
  api.put(`/books/${id}`, book);

export const deleteBook = (id) =>
  api.delete(`/books/${id}`);
```

Do not put API calls directly into large JSX components.

Do not repeat URL strings throughout the application unnecessarily.

Do not create fake API responses when the backend is available.

---

# 18. Book Management

Book fields:

```text
BookId
Title
ISBN
AuthorId
CategoryId
TotalCopies
AvailableCopies
PublishedYear
```

Required frontend functionality:

- List books.
- View a book.
- Add a book.
- Edit a book.
- Delete a book when authorized.
- Search books.
- Display availability.
- Display author/category information when provided by the API.

Do not allow the user to edit server-controlled values unless the backend DTO permits it.

Do not allow a Member to access Add/Edit/Delete operations.

---

# 19. Author Management

Author fields:

```text
AuthorId
AuthorName
```

Admin functionality:

- List authors.
- View author.
- Add author.
- Edit author.
- Delete author.

Do not expose author management to Members or Librarians unless the backend explicitly grants those permissions.

---

# 20. Category Management

Category fields:

```text
CategoryId
CategoryName
```

Admin functionality:

- List categories.
- View category.
- Add category.
- Edit category.
- Delete category.

Do not expose category management to Members or Librarians unless the backend explicitly grants those permissions.

---

# 21. User Management

User fields include:

```text
UserId
FullName
Email
PasswordHash
Role
IsActive
```

The frontend must never display or request `PasswordHash`.

Admin user management may display safe user information such as:

- User ID
- Full name
- Email
- Role
- Active/inactive status

Never expose sensitive password data.

---

# 22. Borrowing

Required operations:

- Issue a book.
- Return a book.
- View member borrowing history.
- View book borrowing history.
- View a member's currently borrowed books.

The assignment defines these business rules:

### Availability

A book cannot be borrowed when:

```text
AvailableCopies = 0
```

### Duplicate borrowing

A member must not borrow the same book while an active borrowing transaction already exists.

### Borrow

Successful borrowing decreases:

```text
AvailableCopies = AvailableCopies - 1
```

### Return

Successful return increases:

```text
AvailableCopies = AvailableCopies + 1
```

The transaction is updated with:

- ReturnDate
- Status

### Invalid return

A book that has already been returned cannot be returned again.

### Delete book

A book with an active borrowing transaction cannot be deleted.

These rules must be enforced by the backend.

The frontend should display the backend's meaningful error response and should not attempt to replace server-side business logic.

---

# 23. Forms and Validation

Create reusable form components where practical.

Validate:

- Required fields.
- Email format.
- Password input.
- Numeric fields.
- Book copies.
- Published year.
- IDs/selections.
- Other constraints exposed by the backend DTOs.

Frontend validation improves user experience.

Backend validation remains authoritative.

Do not duplicate complicated business rules in the frontend.

Display server validation errors clearly.

Avoid generic messages such as:

```text
Something went wrong
```

when the backend provides a useful validation/error message.

---

# 24. Loading, Empty, and Error States

Every data-driven page should handle:

1. Loading state.
2. Successful data state.
3. Empty state.
4. Error state.

Examples:

```text
Loading books...
No books found.
Unable to load books.
```

Buttons that trigger API operations should prevent accidental duplicate submissions while the operation is in progress.

---

# 25. UI/UX Guidelines

Create a professional library management interface.

The UI should be:

- Clean.
- Responsive.
- Consistent.
- Accessible.
- Easy to navigate.
- Suitable for desktop and tablet use.

Use reusable UI components for:

- Buttons
- Inputs
- Selects
- Tables
- Cards
- Modals
- Confirmation dialogs
- Alerts
- Loading indicators
- Empty states

Do not build the same component repeatedly with slightly different markup.

Use semantic HTML.

Provide labels for form controls.

Ensure keyboard-accessible interactions.

Do not rely only on color to communicate status.

---

# 26. Tables

Administrative list pages should use tables where appropriate.

Book table may show:

```text
Title
ISBN
Author
Category
Total Copies
Available Copies
Published Year
Actions
```

Actions must be role-aware.

For example:

- Admin: View, Edit, Delete
- Librarian: View, Edit
- Member: View

The backend must still enforce the same permissions.

---

# 27. Confirmation for Destructive Operations

Deleting data is destructive.

For operations such as deleting a book:

1. Ask for confirmation.
2. Show the item being deleted.
3. Disable the confirmation action while processing.
4. Display success/error feedback.
5. Refresh the data after successful deletion.

Do not silently delete records.

The frontend must handle a backend `409 Conflict` when deletion is blocked by business rules.

---

# 28. Search

Implement book search using the backend search endpoint:

```text
GET /api/books/search?title={title}
```

The search UI should:

- Allow the user to enter a title.
- Show a loading state.
- Display matching books.
- Display an empty result state.
- Handle API errors.

If the backend supports additional filters, they may be added after the basic title search works.

Do not implement client-side searching over an incomplete dataset when a server-side search endpoint is provided.

---

# 29. Dashboard

The dashboard should be role-aware.

## Admin Dashboard

Potential information:

- Total books.
- Total users.
- Available books.
- Borrowed books.
- Borrowing overview.

Only display statistics that can be obtained from the backend.

Do not invent fake statistics.

## Librarian Dashboard

Potential information:

- Available books.
- Borrowed books.
- Issue book action.
- Return book action.
- Borrowing history.

Only use actual backend data.

## Member Dashboard

Potential information:

- Available books.
- My borrowed books.
- My borrowing history.
- Book search.

Do not expose another user's borrowing information.

---

# 30. Data Ownership and Privacy

A Member must only be able to see their own borrowing information.

Do not trust a `memberId` from the browser to determine ownership.

If the backend provides a "my books" or authenticated-user endpoint, use it.

If the backend requires a member ID, the backend must verify that the authenticated user is authorized to access that member's data.

Frontend route protection alone is not sufficient for data ownership.

---

# 31. Security Rules

Never:

- Hard-code passwords.
- Hard-code JWT tokens.
- Log credentials.
- Log authorization tokens.
- Put SQL credentials in React.
- Connect React directly to SQL Server.
- Store database credentials in frontend files.
- Trust frontend role checks as security.
- Bypass backend authorization.
- Disable certificate/security validation merely to make development work unless explicitly requested and clearly documented.

Do not use `dangerouslySetInnerHTML` unless there is a specific justified requirement.

Treat all API data as untrusted input.

---

# 32. CORS

The backend and frontend run as separate applications and will normally use different development URLs/ports.

Example:

```text
Frontend:
http://localhost:5173

Backend:
https://localhost:xxxx
```

If CORS is required, configure it in the ASP.NET Core backend.

Do not attempt to solve backend CORS problems by adding unsafe frontend workarounds.

Do not use public CORS proxies.

The frontend must use the configured backend URL.

---

# 33. Development Environment

Typical frontend commands:

```powershell
npm install
npm run dev
```

The backend should be started independently from its own project directory or through the solution.

Do not assume that:

```powershell
dotnet run
```

from the repository root will start the backend.

The frontend and backend are separate applications and can run simultaneously.

---

# 34. Backend/Frontend Development Order

When implementing the application, follow this order:

## Phase 1 — Inspect backend

Before writing frontend API code:

- Inspect controllers.
- Inspect DTOs.
- Inspect JWT configuration.
- Inspect role names.
- Inspect Swagger.
- Confirm API base URL.

## Phase 2 — Frontend foundation

Create:

- Vite React app.
- Axios.
- React Router.
- Environment configuration.
- Basic application layout.

## Phase 3 — Authentication

Implement:

- Login.
- Register.
- AuthContext.
- JWT handling.
- Logout.
- Protected routes.
- Role routes.

Test:

```text
Valid login
Invalid login
No token
Valid token
```

## Phase 4 — Role-based dashboard

Implement separate role-aware navigation and dashboards.

Test:

```text
Admin
Librarian
Member
```

## Phase 5 — Books

Implement:

- List
- View
- Create
- Update
- Delete
- Search

## Phase 6 — Authors and categories

Implement Admin CRUD.

## Phase 7 — Users

Implement Admin user management.

## Phase 8 — Borrowing

Implement:

- Issue.
- Return.
- Member borrowed books.
- Borrowing history.

## Phase 9 — Error handling and UX

Implement:

- 401.
- 403.
- 404.
- 409.
- 500.
- Loading states.
- Empty states.
- Form errors.
- Confirmation dialogs.

## Phase 10 — Testing

Verify every required assignment scenario.

---

# 35. Required Authorization Demonstrations

The frontend must support testing these scenarios:

```text
Member → Delete Book
Expected: 403 Forbidden

Librarian → Delete Book
Expected: 403 Forbidden

Admin → Delete Book
Expected: Allowed
```

Also test:

```text
No token → protected API
Expected: 401 Unauthorized

Valid token → protected API
Expected: Allowed if role has permission

Invalid role → protected API
Expected: 403 Forbidden
```

The UI should correctly react to the backend responses.

---

# 36. Required Book Tests

Support testing:

- Create book.
- Get books.
- Get book by ID.
- Update book.
- Delete book.
- Get a non-existing book.

Expected non-existing resource behavior should follow the backend's HTTP response, normally:

```text
404 Not Found
```

---

# 37. Required Borrowing Tests

Support testing:

- Borrow available book.
- Borrow unavailable book.
- Borrow same book twice.
- Return book.
- Return already returned book.

The frontend must display the backend's response clearly.

Do not suppress or replace useful backend error messages.

---

# 38. Coding Standards

Use modern React practices.

Prefer:

- Functional components.
- React hooks.
- Small reusable components.
- Descriptive variable names.
- Descriptive function names.
- Early returns where useful.
- Centralized API communication.
- Centralized role constants.
- Centralized authentication state.

Avoid:

- Huge components.
- Duplicate API calls.
- Duplicate permission logic.
- Unnecessary global state.
- Unnecessary dependencies.
- Deeply nested conditional rendering.
- Hard-coded API URLs.
- Hard-coded test users in production code.
- Console logging sensitive data.

Keep components focused on presentation and user interaction.

Keep API logic in service files.

Keep authentication state in AuthContext.

Keep route authorization in route guards.

---

# 39. Error Message Handling

Prefer the backend's meaningful message when safe to display.

For example:

```text
Book is not available.
You already have an active borrowing transaction for this book.
Book cannot be deleted while it has an active borrowing transaction.
```

If no safe backend message is available, provide a user-friendly fallback.

Never display raw stack traces to users.

Never display SQL errors directly to users.

---

# 40. API Response Flexibility

Do not assume every backend response has exactly the same wrapper.

Before implementing code such as:

```javascript
response.data.data
```

inspect the actual backend response.

If the API returns:

```json
[
  ...
]
```

use the array.

If it returns:

```json
{
  "data": [
    ...
  ]
}
```

handle that actual contract.

Do not create unnecessary response transformations unless they simplify a real backend inconsistency.

---

# 41. No Fake Data

Do not create fake books, users, authors, categories, or borrowing transactions to make the UI appear complete.

Use real API data.

Mock data is acceptable only for isolated UI development when explicitly requested, and it must be clearly separated from production API services.

---

# 42. Do Not Modify Backend Unnecessarily

When the task is to build the React frontend:

- Do not modify backend controllers unless required.
- Do not change backend authorization to make the frontend work.
- Do not weaken JWT validation.
- Do not remove `[Authorize]`.
- Do not remove role restrictions.
- Do not bypass API validation.

If the frontend cannot complete an operation because the backend contract is missing, identify the missing backend capability rather than weakening security.

---

# 43. Do Not Duplicate Business Logic

The following must remain backend responsibilities:

- Book availability.
- Duplicate borrowing.
- Return validation.
- Deleting books with active borrowing.
- Authorization.
- Data ownership.
- Database validation.
- Transaction consistency.

The frontend can provide early validation and improve UX, but the backend must remain authoritative.

---

# 44. Accessibility

All forms must have labels.

Buttons must have meaningful accessible names.

Images must have appropriate `alt` text.

Do not use clickable `<div>` elements when a button or link is appropriate.

Keyboard navigation must remain usable.

Modal dialogs must be dismissible and accessible.

---

# 45. Responsive Design

The UI should work reasonably on:

- Desktop.
- Laptop.
- Tablet.

The primary library-management experience is expected to be desktop/tablet friendly.

Do not sacrifice usability for decorative effects.

---

# 46. Environment Files

Create:

```text
.env.example
```

Example:

```env
VITE_API_BASE_URL=https://localhost:xxxx/api
```

Do not commit real secrets.

Do not put database credentials in `.env` files used by the frontend.

Remember: Vite environment variables are exposed to client-side code.

---

# 47. Comments

Write comments only when they explain:

- Why something is implemented a particular way.
- A non-obvious backend compatibility issue.
- A security consideration.
- A temporary workaround.

Do not write comments that merely restate obvious code.

Bad:

```javascript
// Call getBooks
getBooks();
```

Good:

```javascript
// The backend owns filtering, so the frontend uses the server-side search endpoint.
```

---

# 48. Copilot Implementation Behavior

When generating code:

1. Inspect the existing project first.
2. Reuse existing components when possible.
3. Do not overwrite working code unnecessarily.
4. Make small, focused changes.
5. Keep frontend and backend changes separate.
6. Match existing naming conventions.
7. Verify imports after changes.
8. Avoid unused dependencies.
9. Avoid unused variables.
10. Explain important architectural changes briefly.
11. Prefer complete working features over large unfinished scaffolding.

Do not generate the entire application in one uncontrolled change.

Build incrementally.

---

# 49. When API Details Are Unknown

If the backend implementation is not available or an API response is unclear:

Do not invent the response shape.

Instead:

1. Inspect the controller.
2. Inspect the DTO.
3. Inspect Swagger.
4. Search the repository.
5. If still unavailable, clearly identify the missing contract.

Use a temporary adapter only when there is a clear reason.

---

# 50. Definition of Done

A frontend feature is complete only when:

- The correct page exists.
- The route is protected appropriately.
- The correct roles can access it.
- Unauthorized roles cannot access it.
- API calls use the centralized Axios client.
- JWT authentication is applied where required.
- Loading state exists.
- Empty state exists where appropriate.
- Error state exists.
- Form validation exists where appropriate.
- Backend errors are handled.
- The UI does not expose unauthorized actions.
- The backend remains the final authorization authority.
- No sensitive data is logged.
- No unnecessary duplicate code exists.
- The feature works against the actual backend API.

---

# 51. Final Architectural Rule

Always preserve this separation:

```text
┌───────────────────────────────────────┐
│         React Frontend                │
│       library-management-ui           │
│                                       │
│ Pages → Components → Services → Axios │
└──────────────────┬────────────────────┘
                   │
                   │ HTTP/JSON + JWT
                   ▼
┌───────────────────────────────────────┐
│       ASP.NET Core Web API             │
│          LibraryManagement             │
│                                       │
│ Controllers                            │
│      ↓                                │
│ Services / Business Logic             │
│      ↓                                │
│ Repositories                          │
│      ↓                                │
│ EF Core / DbContext                   │
│      ↓                                │
│ SQL Server                            │
└───────────────────────────────────────┘
```

The React application is a client of the API.

The ASP.NET Core API is the security and business-logic authority.

Never bypass this architecture.
