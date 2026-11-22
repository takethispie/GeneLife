using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Genelife.UnitTests;

/// <summary>
/// Hosts the API for tests in an isolated Marten schema and SQLite snapshot file.
/// This prevents projection deadlocks and stale documents leaking between test
/// classes that run in parallel.
/// </summary>
public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "integration-test-signing-key-0123456789abcdef";
    public const string AdminSecret = "integration-test-admin-secret";

    private readonly string schemaName = "test_" + Guid.NewGuid().ToString("N");
    private readonly string snapshotPath = Path.Combine(
        Path.GetTempPath(),
        $"genelife-tests-{Guid.NewGuid():N}.db");

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? Environment.GetEnvironmentVariable("GENELIFE_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Database=genelife_dev;Username=genelife;Password=genelife";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
        builder.UseSetting("Marten:SchemaName", schemaName);
        builder.UseSetting("Persistence:DatabasePath", snapshotPath);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = ConnectionString,
                ["Marten:SchemaName"] = schemaName,
                ["Persistence:DatabasePath"] = snapshotPath,
                ["Auth:Jwt:Issuer"] = "genelife-tests",
                ["Auth:Jwt:Audience"] = "genelife-tests",
                ["Auth:Jwt:Key"] = JwtKey,
                ["Auth:Jwt:AdminSecret"] = AdminSecret,
                ["Auth:Jwt:TokenLifetimeMinutes"] = "60",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        try
        {
            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);
        }
        catch
        {
            // Test cleanup best effort.
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            command.ExecuteNonQuery();
        }
        catch
        {
            // The database may be unavailable in a unit-only run.
        }
    }
}
