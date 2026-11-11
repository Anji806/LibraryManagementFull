using LibraryManagement.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LibraryManagement.Tests;

public class DatabaseConnectionTests
{
    [Fact]
    public void Normalize_NeonStyleUrl_IsConvertedToNpgsqlSettings()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.Normalize(
            "postgresql://neon_user:p%40ss%3Aw%2Frd@ep-cool-123-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        Assert.Equal("ep-cool-123-pooler.eu-central-1.aws.neon.tech", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("neondb", result.Database);
        Assert.Equal("neon_user", result.Username);
        Assert.Equal("p@ss:w/rd", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
        Assert.Equal(ChannelBinding.Require, result.ChannelBinding);
    }

    [Fact]
    public void Normalize_RemoteUrlWithoutSslMode_RequiresTls()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.Normalize("postgres://u:p@db.example.com:6543/library"));

        Assert.Equal(6543, result.Port);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Normalize_LocalUrlWithoutSslMode_DoesNotForceTls()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.Normalize("postgresql://postgres:pw@localhost/library"));

        Assert.NotEqual(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Normalize_KeyValueString_IsAccepted()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.Normalize(
            "Host=h;Database=d;Username=u;Password=p"));

        Assert.Equal("h", result.Host);
        Assert.Equal("d", result.Database);
    }

    [Fact]
    public void Normalize_InvalidValue_FailsWithoutLeakingTheValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnection.Normalize("postgresql://user:topsecret@@@bad host/db"));

        Assert.DoesNotContain("topsecret", ex.Message);
    }

    [Fact]
    public void Resolve_WithoutAnyConfiguration_FailsClearly()
    {
        var previous = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariable);
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariable, null);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DatabaseConnection.Resolve(new ConfigurationBuilder().Build()));

            Assert.Contains("DATABASE_URL", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariable, previous);
        }
    }

    [Fact]
    public void Resolve_PrefersConfiguredConnectionStringOverEnvironmentVariable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=configured;Database=d"
            })
            .Build();

        Assert.Equal("configured",
            new NpgsqlConnectionStringBuilder(DatabaseConnection.Resolve(configuration)).Host);
    }
}
