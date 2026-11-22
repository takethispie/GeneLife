using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Genelife.Api.Persistence;

public sealed class SnapshotStore : ISnapshotStore, IDisposable
{
    private readonly string connectionString;
    private bool initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SnapshotStore(IOptions<SnapshotStoreOptions> options)
    {
        var path = options.Value.DatabasePath;

        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        if (initialized)
            return Task.CompletedTask;

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tick INTEGER NOT NULL,
                schema_version INTEGER NOT NULL,
                snapshot_json TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_snapshots_tick ON snapshots(tick DESC);
            """;
        cmd.ExecuteNonQuery();

        initialized = true;
        return Task.CompletedTask;
    }

    public void Save(PersistedWorldState state)
    {
        if (!initialized)
            throw new InvalidOperationException("Snapshot store is not initialized");

        var json = JsonSerializer.Serialize(state, JsonOptions);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO snapshots (tick, schema_version, snapshot_json) VALUES (@tick, @version, @json)";
        cmd.Parameters.AddWithValue("@tick", state.Tick);
        cmd.Parameters.AddWithValue("@version", state.SchemaVersion);
        cmd.Parameters.AddWithValue("@json", json);
        cmd.ExecuteNonQuery();
    }

    public PersistedWorldState? LoadLatest()
    {
        if (!initialized)
            throw new InvalidOperationException("Snapshot store is not initialized");

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT snapshot_json, schema_version FROM snapshots ORDER BY tick DESC LIMIT 1";
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        var json = reader.GetString(0);
        var schemaVersion = reader.GetInt32(1);

        if (schemaVersion > PersistedWorldState.CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"Snapshot schema version {schemaVersion} is newer than supported version {PersistedWorldState.CurrentSchemaVersion}. "
                + "Upgrade the application or use a compatible snapshot.");

        if (schemaVersion < 1)
            throw new InvalidOperationException(
                $"Snapshot schema version {schemaVersion} is older than current version {PersistedWorldState.CurrentSchemaVersion}. "
                + "Manual migration may be required.");

        var state = JsonSerializer.Deserialize<PersistedWorldState>(json, JsonOptions);
        if (state is null)
            return null;

        if (schemaVersion < PersistedWorldState.CurrentSchemaVersion)
            SnapshotMigrations.Upgrade(state, schemaVersion);

        return state;
    }

    public void Dispose()
    {
        initialized = false;
    }
}