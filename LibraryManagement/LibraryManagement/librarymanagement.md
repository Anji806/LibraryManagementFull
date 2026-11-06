LibraryManagement — Product Requirements Document (PRD)

1. Overview
- Name: LibraryManagement
- Purpose: Manage library catalog, inventory, and borrowing lifecycle with role-based access (Admin, Librarian, Member/Guest).
- Value: Enables librarians to maintain catalog and handle lend/return operations and provides members read/search/borrow history.

2. Goals
- Provide CRUD for books, authors, categories.
- Track inventory (total vs available copies).
- Manage borrow/return transactions and history.
- Secure endpoints with authentication and role-based authorization.
- Provide a simple REST API for frontend or third-party integrations.

3. Scope
- In-scope: Book management, search, borrow/return flows, borrow history, role-based auth.
- Out-of-scope (initial): Reservations/waitlists, fines/payments, offline sync, advanced analytics.

4. Users / Personas
- Admin: full access including delete operations.
- Librarian: create/update books, process borrows/returns.
- Member: view/search catalog, view own borrow history.
- Guest: view/search catalog (read-only).

5. Core Features
- Catalog
  - Add/update/delete books.
  - Search books by title.
  - Get book details with author/category metadata.
- Inventory
  - Track `TotalCopies` and `AvailableCopies`.
  - Prevent reducing `TotalCopies` below active borrows.
- Borrowing
  - Create borrow transactions, mark returns.
  - Query borrow history by book and by member.
  - Prevent deleting books with active borrowings.
- Security
  - JWT-based authentication (or equivalent) and roles: `Admin`, `Librarian`, `Member`.

6. Public API (summary)
- Books
  - `GET /api/books` — list books
  - `GET /api/books/{id}` — get book details
  - `GET /api/books/search?title={q}` — search by title
  - `POST /api/books` — create book (Admin/Librarian)
  - `PUT /api/books/{id}` — update book (Admin/Librarian)
  - `DELETE /api/books/{id}` — delete book (Admin)
  - `GET /api/books/{bookId}/borrow-history` — borrow history
- Borrow Transactions
  - `POST /api/borrow` — create borrow
  - `POST /api/return` — process return
  - `GET /api/borrow/user/{userId}` — user borrow history
  - `GET /api/borrow/book/{bookId}` — book borrow history
- Auth (expected)
  - `POST /api/auth/login`, `POST /api/auth/register`, role management endpoints

7. Data model (key entities)
- `Book`: BookId, Title, ISBN, AuthorId, CategoryId, TotalCopies, AvailableCopies, PublishedYear
- `Author`: AuthorId, AuthorName
- `Category`: CategoryId, CategoryName
- `BorrowTransaction`: TransactionId, BookId, UserId, BorrowDate, DueDate, ReturnDate, Status
- `User`: UserId, Username, Email, Roles

8. Business rules / validations
- On create: `AvailableCopies = TotalCopies`.
- On update: `TotalCopies` must be >= currently borrowed copies.
- Delete blocked when active borrowings exist.
- Search requires non-empty query parameter.
- API returns appropriate HTTP codes: 200, 201, 204, 400, 404, 409.

9. Non-functional requirements
- Response time: typical API responses < 300ms for catalog reads.
- Concurrency: prevent race conditions updating `AvailableCopies` (use DB transactions or optimistic concurrency).
- Persistence: SQL-based relational DB (EF Core).
- Logging & monitoring: errors and important events logged.

10. Security
- Enforce role-based authorization on sensitive endpoints.
- Use HTTPS in production.
- Do not expose passwords or sensitive data in responses.

11. Acceptance criteria
- CRUD operations for books work and enforce validations.
- Search returns matching results; empty title returns 400.
- Cannot delete book with active borrowings (returns 409).
- Updating total copies below borrowed copies returns 400.
- Auth roles restrict endpoints as specified.

12. Suggested milestones
- M1: Core models + EF migrations + basic CRUD for books.
- M2: Authentication, roles and protected endpoints.
- M3: Borrow/return flows + history endpoints + validation.
- M4: Tests, logging, concurrency handling, documentation.

Notes
- Controller reference: `LibraryManagement/Controllers/BooksController.cs`.
- Keep API error responses consistent using a simple object with a `message` field.
