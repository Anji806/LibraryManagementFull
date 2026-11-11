// One-time data copy: SQLite (Dapper-era database) -> PostgreSQL (EF Core schema).
//
//   dotnet run --project LibraryManagement.SqliteToPostgres -- \
//       --sqlite LibraryManagement/Data/application.db [--postgres <url>] [--dry-run]
//
// * The SQLite file is never modified: a copy (with its -wal/-shm files) is read.
// * The PostgreSQL schema must already exist (dotnet ef database update / app start-up).
// * The target tables must be empty; the tool refuses to touch existing data.
// * Everything is copied in ONE transaction with the original IDs, identity
//   sequences are advanced, counts and keys are verified, and only then it commits.

using System.Globalization;
using LibraryManagement.Data;
using Microsoft.Data.Sqlite;
using Npgsql;

var options = Options.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("""
        Usage: dotnet run --project LibraryManagement.SqliteToPostgres -- --sqlite <file> [--postgres <url>] [--dry-run]

          --sqlite    path to the SQLite database (read only; a copy is used)
          --postgres  postgresql:// URL or Npgsql connection string
                      (default: the DATABASE_URL environment variable)
          --dry-run   do everything, verify, then roll back
        """);
    return 2;
}

var sqlitePath = Path.GetFullPath(options.SqlitePath);
if (!File.Exists(sqlitePath))
{
    Console.Error.WriteLine($"SQLite file not found: {sqlitePath}");
    return 2;
}

var postgresRaw = options.PostgresConnection
    ?? Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariable);
if (string.IsNullOrWhiteSpace(postgresRaw))
{
    Console.Error.WriteLine("No PostgreSQL connection: pass --postgres or set DATABASE_URL.");
    return 2;
}

// Parents before children; also the order tables are filled in.
var tables = new Table[]
{
    new("Authors", "AuthorId", Identity: true),
    new("Categories", "CategoryId", Identity: true),
    new("Users", "UserId", Identity: true),
    new("Books", "BookId", Identity: true),
    new("BookCategories", null, Identity: false),
    new("BorrowTransactions", "TransactionId", Identity: true),
    new("LoginHistory", "LoginLogId", Identity: true)
};

// ---- Read a private copy of the SQLite database (original stays untouched).
var workDirectory = Path.Combine(Path.GetTempPath(), "lm-sqlite-copy-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workDirectory);
var copyPath = Path.Combine(workDirectory, "source.db");

try
{
    foreach (var suffix in new[] { "", "-wal", "-shm" })
    {
        var file = sqlitePath + suffix;
        if (File.Exists(file))
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = File.Create(copyPath + suffix);
            input.CopyTo(output);
        }
    }

    await using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = copyPath,
        Pooling = false
    }.ToString());
    await sqlite.OpenAsync();

    await using var postgres = new NpgsqlConnection(DatabaseConnection.Normalize(postgresRaw));
    await postgres.OpenAsync();

    // ---- Pre-flight checks.
    var sqliteTables = (await QueryColumnAsync(sqlite, "SELECT name FROM sqlite_master WHERE type = 'table'"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var required in new[] { "Authors", "Categories", "Users", "Books", "BorrowTransactions" })
    {
        if (!sqliteTables.Contains(required))
        {
            Console.Error.WriteLine($"The SQLite file has no '{required}' table; is it the Library Management database?");
            return 1;
        }
    }

    var orphans = new List<string>();
    await using (var check = sqlite.CreateCommand())
    {
        check.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            orphans.Add($"{reader.GetString(0)} rowid {reader.GetValue(1)} -> {reader.GetString(2)}");
        }
    }

    if (orphans.Count > 0)
    {
        Console.Error.WriteLine("The SQLite data violates its own foreign keys; fix these rows first:");
        orphans.Take(20).ToList().ForEach(o => Console.Error.WriteLine("  " + o));
        return 1;
    }

    foreach (var table in tables)
    {
        var exists = await ScalarAsync<bool>(postgres, null,
            "SELECT to_regclass(@name) IS NOT NULL", ("name", $"\"{table.Name}\""));
        if (!exists)
        {
            Console.Error.WriteLine($"PostgreSQL table \"{table.Name}\" does not exist. Apply the EF Core migrations first (dotnet ef database update).");
            return 1;
        }

        var existing = await ScalarAsync<long>(postgres, null, $"SELECT COUNT(*) FROM \"{table.Name}\"");
        if (existing > 0)
        {
            Console.Error.WriteLine($"PostgreSQL table \"{table.Name}\" already contains {existing} row(s). Refusing to copy into a non-empty database.");
            return 1;
        }
    }

    // ---- Copy everything in one transaction.
    await using var transaction = await postgres.BeginTransactionAsync();
    var report = new List<(string Table, long Source, long Target)>();

    foreach (var table in tables)
    {
        if (!sqliteTables.Contains(table.Name))
        {
            Console.WriteLine($"{table.Name,-20} not in SQLite file, skipped");
            report.Add((table.Name, 0, 0));
            continue;
        }

        var copied = await CopyTableAsync(sqlite, postgres, transaction, table);
        Console.WriteLine($"{table.Name,-20} copied {copied} row(s)");
    }

    // Legacy databases keep each book's primary category in Books.CategoryId;
    // the join table must contain it too (what the old start-up script did).
    await ExecuteAsync(postgres, transaction, """
        INSERT INTO "BookCategories" ("BookId", "CategoryId")
        SELECT "BookId", "CategoryId" FROM "Books"
        ON CONFLICT DO NOTHING
        """);

    // ---- Keep identity sequences ahead of the copied IDs.
    foreach (var table in tables.Where(t => t.Identity))
    {
        await ExecuteAsync(postgres, transaction, $"""
            SELECT setval(
                pg_get_serial_sequence('"{table.Name}"', '{table.Key}'),
                COALESCE((SELECT MAX("{table.Key}") FROM "{table.Name}"), 1),
                EXISTS (SELECT 1 FROM "{table.Name}"))
            """);
    }

    // ---- Verify before committing.
    var failures = new List<string>();
    foreach (var table in tables.Where(t => sqliteTables.Contains(t.Name)))
    {
        var source = await SqliteScalarAsync<long>(sqlite, $"SELECT COUNT(*) FROM \"{table.Name}\"");
        var target = await ScalarAsync<long>(postgres, transaction, $"SELECT COUNT(*) FROM \"{table.Name}\"");
        var ok = table.Name == "BookCategories" ? target >= source : target == source;
        Console.WriteLine($"{table.Name,-20} sqlite={source,-6} postgres={target,-6} {(ok ? "OK" : "MISMATCH")}");
        if (!ok)
        {
            failures.Add($"{table.Name}: sqlite={source}, postgres={target}");
        }

        if (table.Key is not null)
        {
            var sourceKeys = await QueryColumnAsync(sqlite, $"SELECT \"{table.Key}\" FROM \"{table.Name}\" ORDER BY 1");
            var targetKeys = await QueryKeysAsync(postgres, transaction, table);
            if (!sourceKeys.SequenceEqual(targetKeys))
            {
                failures.Add($"{table.Name}: primary key values differ");
            }
        }
    }

    if (failures.Count > 0)
    {
        await transaction.RollbackAsync();
        Console.Error.WriteLine("Verification failed; nothing was written:");
        failures.ForEach(f => Console.Error.WriteLine("  " + f));
        return 1;
    }

    if (options.DryRun)
    {
        await transaction.RollbackAsync();
        Console.WriteLine("Dry run finished: everything verified and rolled back. Nothing was written.");
        return 0;
    }

    await transaction.CommitAsync();
    Console.WriteLine("Done. All rows copied, verified and committed. The SQLite file was not modified.");
    return 0;
}
catch (Exception ex) when (ex is NpgsqlException or SqliteException or InvalidOperationException or IOException)
{
    // Messages from Npgsql never include the password.
    Console.Error.WriteLine("Migration failed and was rolled back: " + ex.Message);
    return 1;
}
finally
{
    try { SqliteConnection.ClearAllPools(); Directory.Delete(workDirectory, recursive: true); }
    catch (IOException) { /* temp copy cleanup is best effort */ }
}

// ======================================================================

static async Task<long> CopyTableAsync(
    SqliteConnection sqlite, NpgsqlConnection postgres, NpgsqlTransaction transaction, Table table)
{
    // Target column metadata decides how each SQLite value is converted.
    var targetColumns = new Dictionary<string, (string Type, bool Nullable)>(StringComparer.OrdinalIgnoreCase);
    await using (var meta = new NpgsqlCommand("""
        SELECT column_name, udt_name, is_nullable = 'YES'
        FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = @table
        """, postgres, transaction))
    {
        meta.Parameters.AddWithValue("table", table.Name);
        await using var reader = await meta.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            targetColumns[reader.GetString(0)] = (reader.GetString(1), reader.GetBoolean(2));
        }
    }

    await using var select = sqlite.CreateCommand();
    select.CommandText = $"SELECT * FROM \"{table.Name}\"" + (table.Key is null ? "" : $" ORDER BY \"{table.Key}\"");
    await using var rows = await select.ExecuteReaderAsync();

    var columns = Enumerable.Range(0, rows.FieldCount).Select(rows.GetName).ToArray();
    var unknown = columns.Where(c => !targetColumns.ContainsKey(c)).ToArray();
    if (unknown.Length > 0)
    {
        throw new InvalidOperationException(
            $"SQLite column(s) {string.Join(", ", unknown)} of {table.Name} have no PostgreSQL column; refusing to drop data.");
    }

    var insert = $"INSERT INTO \"{table.Name}\" ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) " +
                 $"VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})";

    long count = 0;
    while (await rows.ReadAsync())
    {
        await using var command = new NpgsqlCommand(insert, postgres, transaction);
        for (var i = 0; i < columns.Length; i++)
        {
            var (type, nullable) = targetColumns[columns[i]];
            command.Parameters.AddWithValue($"p{i}", Convert(rows.GetValue(i), type, nullable));
        }

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            var key = table.Key is null ? "" : $" ({table.Key}={rows[table.Key]})";
            throw new InvalidOperationException($"{table.Name} row{key}: {ex.MessageText}");
        }

        count++;
    }

    return count;
}

static object Convert(object value, string postgresType, bool nullable)
{
    if (value is DBNull)
    {
        // NOT NULL text columns (e.g. Users.Address) get an empty string.
        return !nullable && postgresType is "text" or "citext" or "varchar" ? "" : DBNull.Value;
    }

    return postgresType switch
    {
        "bool" => System.Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
        "int4" => System.Convert.ToInt32(value, CultureInfo.InvariantCulture),
        "int8" => System.Convert.ToInt64(value, CultureInfo.InvariantCulture),
        "timestamptz" => ParseUtc(value),
        _ => System.Convert.ToString(value, CultureInfo.InvariantCulture)!
    };
}

// SQLite stored these as text; every value written by the application is UTC.
static DateTime ParseUtc(object value)
    => DateTime.Parse(
        System.Convert.ToString(value, CultureInfo.InvariantCulture)!,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

static async Task<List<string>> QueryColumnAsync(SqliteConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var values = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        values.Add(System.Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!);
    }

    return values;
}

static async Task<List<string>> QueryKeysAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Table table)
{
    await using var command = new NpgsqlCommand(
        $"SELECT \"{table.Key}\" FROM \"{table.Name}\" ORDER BY 1", connection, transaction);
    var values = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        values.Add(System.Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!);
    }

    return values;
}

static async Task<T> ScalarAsync<T>(
    NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    foreach (var (name, value) in parameters)
    {
        command.Parameters.AddWithValue(name, value);
    }

    return (T)(await command.ExecuteScalarAsync())!;
}

static async Task<T> SqliteScalarAsync<T>(SqliteConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return (T)System.Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T), CultureInfo.InvariantCulture)!;
}

static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    await command.ExecuteNonQueryAsync();
}

internal sealed record Table(string Name, string? Key, bool Identity);

internal sealed record Options(string SqlitePath, string? PostgresConnection, bool DryRun)
{
    public static Options? Parse(string[] args)
    {
        string? sqlite = null, postgres = null;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sqlite" when i + 1 < args.Length: sqlite = args[++i]; break;
                case "--postgres" when i + 1 < args.Length: postgres = args[++i]; break;
                case "--dry-run": dryRun = true; break;
                default: return null;
            }
        }

        return sqlite is null ? null : new Options(sqlite, postgres, dryRun);
    }
}
