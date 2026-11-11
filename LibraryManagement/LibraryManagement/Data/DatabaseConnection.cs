using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LibraryManagement.Data;

/// <summary>
/// Resolves the PostgreSQL connection string. The value comes from
/// ConnectionStrings:DefaultConnection (user secrets, appsettings or the
/// ConnectionStrings__DefaultConnection environment variable) and falls back
/// to the DATABASE_URL environment variable. Both a Npgsql key/value string
/// and a postgres:// / postgresql:// URL (the format Neon and Render hand
/// out) are accepted.
/// </summary>
public static class DatabaseConnection
{
    public const string EnvironmentVariable = "DATABASE_URL";

    public static string Resolve(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable(EnvironmentVariable);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "No database connection string is configured. Set the " +
                $"{EnvironmentVariable} environment variable (for example " +
                "'postgresql://user:password@host/dbname?sslmode=require') " +
                "or ConnectionStrings:DefaultConnection.");
        }

        return Normalize(raw);
    }

    public static string Normalize(string raw)
    {
        raw = raw.Trim();

        var isUrl =
            raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        try
        {
            return isUrl
                ? FromUrl(raw)
                : new NpgsqlConnectionStringBuilder(raw).ConnectionString;
        }
        catch (Exception ex) when (
            ex is UriFormatException or ArgumentException or FormatException)
        {
            // Deliberately omit the value: it contains credentials.
            throw new InvalidOperationException(
                "The database connection string is not valid. Expected " +
                "'postgresql://user:password@host[:port]/dbname[?sslmode=require]' " +
                "or a Npgsql 'Host=...;Database=...;Username=...;Password=...' string.");
        }
    }

    private static string FromUrl(string url)
    {
        var uri = new Uri(url);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Timeout = 30 // serverless databases (Neon) may need to wake up
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(
                separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);

            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(
                    uri.UserInfo[(separator + 1)..]);
            }
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        var explicitSsl = false;

        foreach (var (key, values) in query)
        {
            var value = values.ToString();

            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    builder.SslMode = Enum.Parse<SslMode>(
                        value.Replace("-", ""), ignoreCase: true);
                    explicitSsl = true;
                    break;

                case "channel_binding":
                    builder.ChannelBinding = Enum.Parse<ChannelBinding>(
                        value, ignoreCase: true);
                    break;

                case "connect_timeout":
                    builder.Timeout = int.Parse(value);
                    break;

                default:
                    try { builder[key] = value; }
                    catch (ArgumentException) { /* unsupported libpq option */ }
                    break;
            }
        }

        if (!explicitSsl && !uri.IsLoopback)
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }
}
