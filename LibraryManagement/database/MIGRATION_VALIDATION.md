# SQLite migration validation report

Status: NOT RUN TO COMPLETION. A copy was attempted against the original `localhost / LibraryManagementDb` SQL Server setting. Connection attempts under the current `codexsandboxoffline` process failed before opening the SQLite destination: encrypted connection reported that encryption was unsupported, and `Encrypt=False` attempts failed Windows SSPI authentication (`Cannot generate SSPI context`). No SQL Server rows were read and the schema-only SQLite file remains unchanged.

| Table | SQL Server count | SQLite count | Migration status | Errors |
|---|---:|---:|---|---|
| Authors | N/A | 0 | Pending source access | SQL Server authentication failed before query |
| Categories | N/A | 0 | Pending source access | SQL Server authentication failed before query |
| Users | N/A | 0 | Pending source access | SQL Server authentication failed before query |
| Books | N/A | 0 | Pending source access | SQL Server authentication failed before query |
| BorrowTransactions | N/A | 0 | Pending source access | SQL Server authentication failed before query |
| LoginHistory | N/A | 0 | Pending source access | SQL Server authentication failed before query |

No data has been copied. Retry from an account with read access to the source SQL Server by setting `MIGRATION_SQLSERVER_CONNECTION` in that process environment, then run the utility documented in `DATABASE_OPERATIONS.md`. The utility does not modify SQL Server.
