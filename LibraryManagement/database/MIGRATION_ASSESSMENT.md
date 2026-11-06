# SQL Server to SQLite Migration Assessment

Assessment date: 2026-11-05
Scope inspected: ASP.NET Core API, Dapper repositories, models, EF Core schema/migrations, database scripts, API tests, and React package manifest. No source code was changed before this assessment was completed.

## Current database wiring

- Runtime database registration is in `LibraryManagement/Program.cs`. It registers a singleton `IDbConnectionFactory` backed by `SqlConnectionFactory` and the `DefaultConnection` setting.
- `DapperUnitOfWork` owns one open connection per scoped unit of work and runs queued mutations in transactions.
- `appsettings.json` points to local SQL Server database `LibraryManagementDb` with Windows authentication.
- EF Core is not registered in API runtime, but the API project still references EF Core Design, SqlServer, and Tools; `ApplicationDbContext`, SQL Server design-time factory, and SQL Server EF migrations remain. Tests seed using EF Core SQLite.
- There is no SQL Server instance/connection secret or existing database file in the inspected workspace, so schema inspection against the live database, data export, and source/target count validation cannot be performed here.

## Dapper, services, and SQL inventory

Dapper repositories: Auth, User, Book, Author, Category, BorrowTransaction, and LoginHistory. They share `IDapperUnitOfWork`; repositories do not each construct connections. Services include authentication, borrowing, and login history. Controllers cover auth, users/members, books, authors, categories, borrowing, and login history. JWT validation and role authorization are independent of the database provider and use `Users` records.

CRUD, joins, parameters, `COUNT`, `IS NULL`, and ordinary `LIKE` queries are compatible with SQLite. IDs for Authors, Categories, Users, Books, BorrowTransactions, and LoginHistory rely on SQL Server `SCOPE_IDENTITY()` (LoginHistory embeds this directly). LoginHistory pagination uses `OFFSET ... FETCH NEXT`; username search uses SQL Server `+` string concatenation. LoginHistory insert and paging are therefore SQL Server-specific. The API has no stored procedure calls, table hints, `MERGE`, `OUTPUT INSERTED`, `GETDATE`, `ISNULL`, `TOP`, `DATEADD`, `DATEDIFF`, `LEN`, or `NEWID` usage. The standalone SQL scripts contain SQL Server-only `TOP`, `dbo`, `IDENTITY`, `NVARCHAR`, `DATETIME2`, `SYSUTCDATETIME`, `OBJECT_ID`, `COL_LENGTH`, `THROW`, table variables, and T-SQL control flow; they are maintenance scripts, not runtime Dapper queries.

## Schema and relationships found

| Table | Key and generated ID | Constraints and relationships |
|---|---|---|
| Authors | `AuthorId int IDENTITY` PK | One-to-many Books; delete restricted by FK |
| Categories | `CategoryId int IDENTITY` PK | One-to-many Books; delete restricted by FK |
| Users | `UserId int IDENTITY` PK | Unique Email; Address added by migration |
| Books | `BookId int IDENTITY` PK | Unique ISBN; AuthorId and CategoryId FKs, both restricted; preview text/cover columns added by startup initializer/script |
| BorrowTransactions | `TransactionId int IDENTITY` PK | BookId required/restricted; MemberId nullable, FK to Users with `SET NULL`; MemberName/MemberEmail preserve snapshots |
| LoginHistory | `LoginLogId bigint IDENTITY` PK | Nullable UserId FK with `SET NULL`; LoginStatus and FailureReason CHECK constraints; three descending/query indexes in SQL script |

Models use `int` for the first five generated keys, `long` for LoginLogId, .NET `DateTime` for borrow and login times, `bool` for IsActive, and strings for text. No GUID, decimal, or binary columns appear in the inspected application model/migrations. EF migrations describe current application history, but no live catalog metadata was available to confirm drift.

## SQLite conversion work required

- Use `Microsoft.Data.Sqlite` and `SqliteConnection`; identity queries become `SELECT last_insert_rowid()` on the same connection/transaction.
- Create INTEGER PRIMARY KEY AUTOINCREMENT keys (including LoginLogId), TEXT for strings/ISO timestamps, and INTEGER 0/1 for booleans. SQLite has no fixed decimal/date storage class; preserve timestamps in a consistent UTC ISO-8601 representation and use integer minor units or canonical decimal text if future exact-decimal columns are added.
- Enable `PRAGMA foreign_keys=ON` for every connection; preserve unique indexes, FKs, SET NULL/RESTRICT actions, checks, and LoginHistory indexes explicitly.
- Replace SQL Server startup DDL with a versioned/idempotent SQLite schema initializer. The existing preview initializer already branches on provider but its SQL Server fallback must be removed once migration is complete.
- Replace LoginHistory paging with `LIMIT @PageSize OFFSET @Offset`, and username matching with SQLite concatenation (`||`) or a bound pattern. Keep the current whitelisted sort direction and parameters.
- Keep all repository query parameters. The current app SQL mostly already uses parameters; its small string interpolation is limited to fixed SQL clauses/selected sort direction.

## Authentication, API, frontend, and deployment

JWT signing/validation and controller routes are provider-independent. Registration, login, user lookup, and login audit operations use Dapper, so the connection/identity/schema changes are the direct auth impact. Existing LoginHistory work is uncommitted and includes SQL Server-only repository SQL and DDL. React package structure is present and no database-specific frontend requirement was found; no API response change is indicated. There is no deployment container/pipeline or backup/restore procedure in the inspected tracked project. A persistent data path, ignored database files, busy timeout, foreign-key enforcement, WAL policy, backup command, and SQL Server rollback instructions need documentation/configuration.

## Verification limits and risks

The repository includes API tests whose fixture uses EF Core SQLite as a seed helper, while the API itself uses Dapper. A Dapper-only production data layer can remain; the fixture should eventually exercise the same SQLite schema initialization and repository operations. A live migration report needs the source SQL Server reachable and credentials supplied through deployment secrets. Until then, no claim can be made about source record counts, source-only schema objects, or successful preservation of real data. The SQL Server original should remain untouched and be kept as the rollback source. The workspace already contains unrelated/uncommitted LoginHistory edits, which must be preserved during migration work.

## Collation compatibility decision

The live SQL Server database collation was not available for inspection. For SQLite, Email and ISBN unique indexes use `NOCASE`, and authentication email lookup uses `NOCASE`, matching the common case-insensitive behavior of SQL Server defaults for the application's ASCII email/ISBN values. SQLite `NOCASE` only folds ASCII; non-ASCII collation behavior may differ from a SQL Server accent/case-insensitive collation and should be confirmed against actual production data before cutover.
