using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LibraryManagement.Data;

/// <summary>
/// Used by the EF Core tools (dotnet ef migrations / database update).
/// Reads DATABASE_URL (or ConnectionStrings:DefaultConnection) exactly like
/// the application does. `migrations add` never connects, so a placeholder
/// is used when nothing is configured; `database update` needs the real value.
/// </summary>
public class DesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString;

        try
        {
            connectionString = DatabaseConnection.Resolve(configuration);
        }
        catch (InvalidOperationException) when (
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    DatabaseConnection.EnvironmentVariable)) &&
            string.IsNullOrWhiteSpace(
                configuration.GetConnectionString("DefaultConnection")))
        {
            Console.Error.WriteLine(
                "DATABASE_URL is not set; using a placeholder connection. " +
                "This is fine for 'migrations add' but 'database update' " +
                "will fail until DATABASE_URL is set.");

            connectionString =
                "Host=localhost;Database=librarymanagement_design;Username=postgres";
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
