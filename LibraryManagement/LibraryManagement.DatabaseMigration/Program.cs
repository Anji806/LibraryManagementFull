using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

var sourceConnectionString = Environment.GetEnvironmentVariable("MIGRATION_SQLSERVER_CONNECTION");
if (args.Length > 0 && args[0] == "--inspect-authors")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet run -- --inspect-authors <sqlite-file>");
        return 2;
    }

    await using var inspectDatabase = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(args[1]),
        Mode = SqliteOpenMode.ReadOnly,
        ForeignKeys = true
    }.ToString());
    await inspectDatabase.OpenAsync();
    Console.WriteLine($"Author count: {await ScalarLongAsync(inspectDatabase, "SELECT COUNT(*) FROM Authors;")}");
    await using var authorsCommand = inspectDatabase.CreateCommand();
    authorsCommand.CommandText = "SELECT AuthorId, AuthorName FROM Authors ORDER BY AuthorId;";
    await using var authorsReader = await authorsCommand.ExecuteReaderAsync();
    while (await authorsReader.ReadAsync())
        Console.WriteLine($"{authorsReader.GetInt64(0)}\t{authorsReader.GetString(1)}");
    return 0;
}

if (args.Length > 0 && args[0] == "--initialize-only")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet run -- --initialize-only <sqlite-file> [sqlite-schema.sql]");
        return 2;
    }

    var emptyDatabasePath = Path.GetFullPath(args[1]);
    var emptySchemaPath = args.Length > 2
        ? Path.GetFullPath(args[2])
        : Path.Combine(AppContext.BaseDirectory, "database", "sqlite_schema.sql");
    Directory.CreateDirectory(Path.GetDirectoryName(emptyDatabasePath)!);
    await using var emptyDatabase = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = emptyDatabasePath,
        ForeignKeys = true,
        DefaultTimeout = 30
    }.ToString());
    await emptyDatabase.OpenAsync();
    await ExecuteAsync(emptyDatabase, "PRAGMA journal_mode=WAL;");
    await ExecuteAsync(emptyDatabase, "PRAGMA foreign_keys=ON;");
    await ExecuteAsync(emptyDatabase, "PRAGMA busy_timeout=30000;");
    await ExecuteAsync(emptyDatabase, await File.ReadAllTextAsync(emptySchemaPath));
    Console.WriteLine($"Initialized SQLite schema at {emptyDatabasePath} (no source data copied).");
    return 0;
}

if (string.IsNullOrWhiteSpace(sourceConnectionString) || args.Length < 1)
{
    Console.Error.WriteLine("Set MIGRATION_SQLSERVER_CONNECTION, then run: dotnet run -- <sqlite-file> [sqlite-schema.sql] [validation-report.csv]");
    return 2;
}

var sqlitePath = Path.GetFullPath(args[0]);
var schemaPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(AppContext.BaseDirectory, "database", "sqlite_schema.sql");
var reportPath = args.Length > 2
    ? Path.GetFullPath(args[2])
    : Path.Combine(Path.GetDirectoryName(sqlitePath)!, "migration-validation.csv");

if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine($"SQLite schema script was not found: {schemaPath}");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
var sqliteConnectionString = new SqliteConnectionStringBuilder
{
    DataSource = sqlitePath,
    ForeignKeys = true,
    DefaultTimeout = 30
}.ToString();

var tables = new (string Name, string Key)[]
{
    ("Authors", "AuthorId"),
    ("Categories", "CategoryId"),
    ("Users", "UserId"),
    ("Books", "BookId"),
    ("BorrowTransactions", "TransactionId"),
    ("LoginHistory", "LoginLogId")
};
var results = new List<(string Table, long Source, long Target, string Status, string Error)>();

await using var source = new SqlConnection(sourceConnectionString);
await source.OpenAsync();
await using var target = new SqliteConnection(sqliteConnectionString);
await target.OpenAsync();
await ExecuteAsync(target, "PRAGMA journal_mode=WAL;");
await ExecuteAsync(target, "PRAGMA foreign_keys=ON;");
await ExecuteAsync(target, "PRAGMA busy_timeout=30000;");
await ExecuteAsync(target, await File.ReadAllTextAsync(schemaPath));

foreach (var (table, key) in tables)
{
    long sourceCountForReport = -1;
    long targetCountForReport = -1;
    try
    {
        var quotedTable = $"[{table}]";
        var sourceCount = (long)(await new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{quotedTable}", source).ExecuteScalarAsync()
            ?? throw new InvalidOperationException("SQL Server returned no table count."));
        sourceCountForReport = sourceCount;

        await using var select = new SqlCommand($"SELECT * FROM dbo.{quotedTable} ORDER BY [{key}]", source);
        await using var reader = await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        if (!columns.Contains(key, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Primary key column '{key}' was not found.");

        var quotedColumns = string.Join(", ", columns.Select(QuoteSqlite));
        var placeholders = string.Join(", ", columns.Select((_, i) => $"$p{i}"));
        var updateColumns = columns.Where(c => !c.Equals(key, StringComparison.OrdinalIgnoreCase));
        var conflict = updateColumns.Any()
            ? $"DO UPDATE SET {string.Join(", ", updateColumns.Select(c => $"{QuoteSqlite(c)}=excluded.{QuoteSqlite(c)}"))}"
            : "DO NOTHING";
        var insertSql = $"INSERT INTO {QuoteSqlite(table)} ({quotedColumns}) VALUES ({placeholders}) ON CONFLICT({QuoteSqlite(key)}) {conflict}";
        var sourceKeys = new List<string>();

        await using var transaction = await target.BeginTransactionAsync();
        await using var insert = target.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = insertSql;
        for (var i = 0; i < columns.Length; i++)
            insert.Parameters.Add(new SqliteParameter($"$p{i}", DBNull.Value));

        while (await reader.ReadAsync())
        {
            var recordKey = reader[key];
            sourceKeys.Add(Convert.ToString(recordKey, CultureInfo.InvariantCulture)!);
            for (var i = 0; i < columns.Length; i++)
                insert.Parameters[i].Value = Normalize(reader.GetValue(i));
            try
            {
                await insert.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Table '{table}', record key '{recordKey}': {ex.Message}", ex);
            }
        }

        await transaction.CommitAsync();
        var targetCount = await ScalarLongAsync(target, $"SELECT COUNT(*) FROM {QuoteSqlite(table)}");
        targetCountForReport = targetCount;
        var targetKeys = new List<string>();
        await using (var keysCommand = target.CreateCommand())
        {
            keysCommand.CommandText = $"SELECT {QuoteSqlite(key)} FROM {QuoteSqlite(table)} ORDER BY {QuoteSqlite(key)}";
            await using var keysReader = await keysCommand.ExecuteReaderAsync();
            while (await keysReader.ReadAsync())
                targetKeys.Add(Convert.ToString(keysReader.GetValue(0), CultureInfo.InvariantCulture)!);
        }
        if (!sourceKeys.SequenceEqual(targetKeys, StringComparer.Ordinal))
            throw new InvalidOperationException($"{table} primary key values differ between SQL Server and SQLite.");
        var status = sourceCount == targetCount ? "OK" : "COUNT MISMATCH";
        results.Add((table, sourceCount, targetCount, status, ""));
        Console.WriteLine($"{table}: source={sourceCount}, sqlite={targetCount}, {status}");
        if (status != "OK")
        {
            await File.WriteAllLinesAsync(reportPath, ToCsv(results));
            return 1;
        }
    }
    catch (Exception ex)
    {
        if (targetCountForReport < 0)
        {
            try { targetCountForReport = await ScalarLongAsync(target, $"SELECT COUNT(*) FROM {QuoteSqlite(table)}"); }
            catch { /* The table may not have been created yet. */ }
        }
        results.Add((table, sourceCountForReport, targetCountForReport, "ERROR", ex.Message));
        Console.Error.WriteLine($"{table}: ERROR: {ex.Message}");
        await File.WriteAllLinesAsync(reportPath, ToCsv(results));
        return 1;
    }
}

await using var foreignKeyCheck = target.CreateCommand();
foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
await using var violations = await foreignKeyCheck.ExecuteReaderAsync();
if (violations.HasRows)
{
    results.Add(("FOREIGN_KEY_CHECK", -1, -1, "ERROR", "SQLite foreign_key_check reported violations."));
    await File.WriteAllLinesAsync(reportPath, ToCsv(results));
    Console.Error.WriteLine("SQLite foreign_key_check reported violations.");
    return 1;
}
await File.WriteAllLinesAsync(reportPath, ToCsv(results));
Console.WriteLine($"Validation report: {reportPath}");
return 0;

static object Normalize(object value) => value switch
{
    DBNull => DBNull.Value,
    DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
    DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
    decimal number => number.ToString(CultureInfo.InvariantCulture),
    TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
    _ => value
};

static string QuoteSqlite(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

static IEnumerable<string> ToCsv(IEnumerable<(string Table, long Source, long Target, string Status, string Error)> rows)
{
    yield return "Table,SQLServerCount,SQLiteCount,Status,Error";
    foreach (var row in rows)
        yield return string.Join(",", Csv(row.Table), row.Source < 0 ? "" : row.Source.ToString(CultureInfo.InvariantCulture), row.Target < 0 ? "" : row.Target.ToString(CultureInfo.InvariantCulture), Csv(row.Status), Csv(row.Error));
}

static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";



static async Task ExecuteAsync(SqliteConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}




