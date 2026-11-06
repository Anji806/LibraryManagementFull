# Database migration and operations

## SQLite runtime

The API uses Dapper and `Microsoft.Data.Sqlite`. By default, the database lives at `LibraryManagement/Data/application.db`, resolved from the API content root. Set `ConnectionStrings__DefaultConnection` to a writable persistent path in production. Keep the database and its `-wal` / `-shm` companions out of source control. SQLite foreign keys, a 30-second busy timeout, and WAL journaling are enabled by the API at connection/startup.

The API creates the SQLite schema idempotently from `database/sqlite_schema.sql`. Back up a consistent snapshot with SQLite's online backup API or `VACUUM INTO` while the service is in a controlled maintenance window. Restore by stopping the API, copying the backup to the configured path, ensuring the service account can read/write the directory, then restarting and checking application logs. Back up the database file together with WAL state only after a clean checkpoint/shutdown.

## SQL Server to SQLite data copy

The repository does not contain SQL Server credentials or a reachable source server, so no production records have been copied by this change. Create a verified SQL Server backup first. Run the migration utility from a trusted environment with read-only SQL Server access and a destination SQLite path. The source database is only read; never use a destructive source operation. The utility must compare counts and report per-table failures before deployment. Keep SQL Server intact through the rollback window.

## Rollback

Stop the API, preserve a copy of the SQLite database for diagnosis, restore the deployment's prior SQL Server connection configuration and SQL Server API build, then restart against the untouched SQL Server database. Do not dual-write or attempt two-way synchronization. Writes made only after the SQLite cutover are not present in SQL Server; reconcile them operationally before rollback if they must be retained.

## Run the migration utility

Build once, then set `MIGRATION_SQLSERVER_CONNECTION` in the trusted environment (use a read-only SQL Server account) and run:

```powershell
$env:MIGRATION_SQLSERVER_CONNECTION = '<read-only SQL Server connection string>'
dotnet run --project LibraryManagement.DatabaseMigration -- 'D:\persistent-data\application.db' 'database\sqlite_schema.sql' 'D:\persistent-data\migration-validation.csv'
Remove-Item Env:\MIGRATION_SQLSERVER_CONNECTION
```

The tool reads the six known tables in FK order, uses idempotent key-based upserts inside per-table transactions, then checks source/target row counts and SQLite foreign keys. A failed table is recorded with its current row key and stops the run; successfully committed earlier tables remain available for a safe resume. After migration, inspect the CSV and run the API tests before switching traffic. The utility does not write to the SQL Server source.

SQLite's default text collation differs from typical SQL Server case-insensitive database collations. The schema uses `NOCASE` uniqueness for Email and ISBN, and authentication email lookup uses the same collation so common ASCII case variants retain expected behavior.

To create a schema-only database without SQL Server, run:

```powershell
dotnet run --project LibraryManagement.DatabaseMigration -- --initialize-only 'LibraryManagement\Data\application.db' 'database\sqlite_schema.sql'
```

This creates the file and schema but does not copy any records. The checked-out workspace now has this empty schema database at `LibraryManagement/Data/application.db`; it is ignored by Git because it is application data.
