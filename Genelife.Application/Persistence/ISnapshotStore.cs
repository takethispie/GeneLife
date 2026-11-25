namespace Genelife.Api.Persistence;

public interface ISnapshotStore
{
    Task InitializeAsync(CancellationToken ct = default);
    void Save(PersistedWorldState state);
    PersistedWorldState? LoadLatest();
}