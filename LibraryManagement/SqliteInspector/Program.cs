
using Microsoft.Data.Sqlite;
using Npgsql;
using NpgsqlTypes;

const string databasePath =
    @"D:\Anji\Libray\LibraryManagement\LibraryManagement\Data\application.db";

var raw = Environment.GetEnvironmentVariable("NEON_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(raw))
{
    Console.WriteLine("Neon connection string is missing.");
    return;
}

string connectionString;

if (raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
    || raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(raw);
    var credentials = uri.UserInfo.Split(':', 2);

    if (credentials.Length != 2)
        throw new InvalidOperationException("Invalid Neon connection URL.");

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = SslMode.Require
    };

    connectionString = builder.ConnectionString;
}
else
{
    connectionString = raw;
}

var authors = new List<(int Id, string Name)>();
var categories = new List<(int Id, string Name)>();
var books = new List<Book>();
var bookCategories = new List<(int BookId, int CategoryId)>();

// Read the SQLite source without modifying it.
var sqliteConnectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadOnly
}.ToString();

await using (var source = new SqliteConnection(sqliteConnectionString))
{
    await source.OpenAsync();

    await using (var cmd = source.CreateCommand())
    {
        cmd.CommandText = """
            SELECT "AuthorId", "AuthorName"
            FROM "Authors" ORDER BY "AuthorId"
            """;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            authors.Add((reader.GetInt32(0), reader.GetString(1)));
    }

    await using (var cmd = source.CreateCommand())
    {
        cmd.CommandText = """
            SELECT "CategoryId", "CategoryName"
            FROM "Categories" ORDER BY "CategoryId"
            """;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            categories.Add((reader.GetInt32(0), reader.GetString(1)));
    }

    await using (var cmd = source.CreateCommand())
    {
        cmd.CommandText = """
            SELECT "BookId", "Title", "ISBN", "AuthorId", "CategoryId",
                   "TotalCopies", "AvailableCopies", "PublishedYear",
                   "PreviewIntroduction", "PreviewTheme", "PreviewAbstract",
                   "PreviewMotto", "PreviewWhyRead", "CoverUrl"
            FROM "Books" ORDER BY "BookId"
            """;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            books.Add(new Book(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                NullableString(reader, 8),
                NullableString(reader, 9),
                NullableString(reader, 10),
                NullableString(reader, 11),
                NullableString(reader, 12),
                NullableString(reader, 13)
            ));
        }
    }

    await using (var cmd = source.CreateCommand())
    {
        cmd.CommandText = """
            SELECT "BookId", "CategoryId"
            FROM "BookCategories"
            ORDER BY "BookId", "CategoryId"
            """;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            bookCategories.Add((reader.GetInt32(0), reader.GetInt32(1)));
    }
}

Console.WriteLine("SOURCE COUNTS");
Console.WriteLine($"Authors: {authors.Count}");
Console.WriteLine($"Categories: {categories.Count}");
Console.WriteLine($"Books: {books.Count}");
Console.WriteLine($"BookCategories: {bookCategories.Count}");

if (authors.Count != 6 || categories.Count != 8
    || books.Count != 8 || bookCategories.Count != 12)
{
    Console.WriteLine("STOP: Source counts differ from the verified counts.");
    return;
}

// Connect to Neon and perform the import in one transaction.
try
{
    await using var destination =
        new NpgsqlConnection(connectionString);

    await destination.OpenAsync();

    await using var transaction =
        await destination.BeginTransactionAsync();

    try
    {
        var tables = new[]
        {
            "Authors", "Categories", "Books", "BookCategories"
        };

        foreach (var table in tables)
        {
            await using var check = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM public.\"{table}\"",
                destination, transaction);

            var count = Convert.ToInt64(
                await check.ExecuteScalarAsync());

            if (count != 0)
                throw new InvalidOperationException(
                    $"STOP: Neon table {table} is not empty.");
        }

        // Insert Authors.
        foreach (var item in authors)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO public."Authors"
                    ("AuthorId", "AuthorName")
                VALUES (@id, @name)
                """, destination, transaction);

            AddInt(cmd, "id", item.Id);
            AddText(cmd, "name", item.Name);

            await cmd.ExecuteNonQueryAsync();
        }

        // Insert Categories.
        foreach (var item in categories)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO public."Categories"
                    ("CategoryId", "CategoryName")
                VALUES (@id, @name)
                """, destination, transaction);

            AddInt(cmd, "id", item.Id);
            AddText(cmd, "name", item.Name);

            await cmd.ExecuteNonQueryAsync();
        }

        // Insert Books.
        foreach (var item in books)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO public."Books"
                (
                    "BookId", "Title", "ISBN", "AuthorId", "CategoryId",
                    "TotalCopies", "AvailableCopies", "PublishedYear",
                    "PreviewIntroduction", "PreviewTheme", "PreviewAbstract",
                    "PreviewMotto", "PreviewWhyRead", "CoverUrl"
                )
                VALUES
                (
                    @id, @title, @isbn::citext, @authorId, @categoryId,
                    @total, @available, @year,
                    @introduction, @theme, @abstract,
                    @motto, @whyRead, @cover
                )
                """, destination, transaction);

            AddInt(cmd, "id", item.Id);
            AddText(cmd, "title", item.Title);
            AddText(cmd, "isbn", item.ISBN);
            AddInt(cmd, "authorId", item.AuthorId);
            AddInt(cmd, "categoryId", item.CategoryId);
            AddInt(cmd, "total", item.TotalCopies);
            AddInt(cmd, "available", item.AvailableCopies);
            AddNullableInt(cmd, "year", item.PublishedYear);
            AddText(cmd, "introduction", item.PreviewIntroduction);
            AddText(cmd, "theme", item.PreviewTheme);
            AddText(cmd, "abstract", item.PreviewAbstract);
            AddText(cmd, "motto", item.PreviewMotto);
            AddText(cmd, "whyRead", item.PreviewWhyRead);
            AddText(cmd, "cover", item.CoverUrl);

            await cmd.ExecuteNonQueryAsync();
        }

        // Insert BookCategories relationships.
        foreach (var item in bookCategories)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO public."BookCategories"
                    ("BookId", "CategoryId")
                VALUES (@bookId, @categoryId)
                """, destination, transaction);

            AddInt(cmd, "bookId", item.BookId);
            AddInt(cmd, "categoryId", item.CategoryId);

            await cmd.ExecuteNonQueryAsync();
        }

        // Verify imported counts before committing.
        foreach (var table in tables)
        {
            await using var cmd = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM public.\"{table}\"",
                destination, transaction);

            var count = Convert.ToInt64(
                await cmd.ExecuteScalarAsync());

            var expected = table switch
            {
                "Authors" => 6,
                "Categories" => 8,
                "Books" => 8,
                "BookCategories" => 12,
                _ => 0
            };

            if (count != expected)
                throw new InvalidOperationException(
                    $"Count verification failed for {table}: {count}");
        }

        // Synchronize generated ID sequences.
        foreach (var item in new[]
        {
            ("Authors", "AuthorId"),
            ("Categories", "CategoryId"),
            ("Books", "BookId")
        })
        {
            await using var sequenceCommand = new NpgsqlCommand(
                $"SELECT pg_get_serial_sequence('public.\"{item.Item1}\"', '{item.Item2}')",
                destination, transaction);

            var sequence = await sequenceCommand.ExecuteScalarAsync()
                as string;

            if (!string.IsNullOrWhiteSpace(sequence))
            {
                await using var maxCommand = new NpgsqlCommand(
                    $"SELECT MAX(\"{item.Item2}\") FROM public.\"{item.Item1}\"",
                    destination, transaction);

                var maxId = Convert.ToInt64(
                    await maxCommand.ExecuteScalarAsync());

                await using var setCommand = new NpgsqlCommand(
                    "SELECT setval(@sequence::regclass, @maxId, true)",
                    destination, transaction);

                setCommand.Parameters.AddWithValue("sequence", sequence);
                setCommand.Parameters.AddWithValue("maxId", maxId);

                await setCommand.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();

        Console.WriteLine("\nIMPORT SUCCESSFUL");
        Console.WriteLine("Authors: 6");
        Console.WriteLine("Categories: 8");
        Console.WriteLine("Books: 8");
        Console.WriteLine("BookCategories: 12");
        Console.WriteLine("Production Users were not modified.");
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
catch (Exception ex)
{
    Console.WriteLine("\nIMPORT STOPPED. No partial import was committed.");
    Console.WriteLine(ex.Message);
}

static string? NullableString(SqliteDataReader reader, int ordinal) =>
    reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

static void AddInt(NpgsqlCommand cmd, string name, int value)
{
    cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Integer)
    {
        Value = value
    });
}

static void AddNullableInt(NpgsqlCommand cmd, string name, int? value)
{
    cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Integer)
    {
        Value = value.HasValue ? value.Value : DBNull.Value
    });
}

static void AddText(NpgsqlCommand cmd, string name, string? value)
{
    cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
    {
        Value = value is null ? DBNull.Value : value
    });
}

record Book(
    int Id,
    string Title,
    string ISBN,
    int AuthorId,
    int CategoryId,
    int TotalCopies,
    int AvailableCopies,
    int? PublishedYear,
    string? PreviewIntroduction,
    string? PreviewTheme,
    string? PreviewAbstract,
    string? PreviewMotto,
    string? PreviewWhyRead,
    string? CoverUrl
);
