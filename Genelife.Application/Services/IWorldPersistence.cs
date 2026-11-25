using Arch.Core;
using Genelife.Components.Employment;

namespace Genelife.Api.Services;

/// <summary>
/// Marker for a consistent capture of the world document state. The concrete
/// batch is owned by the persistence implementation so the application layer
/// never sees the storage-specific document types.
/// </summary>
public interface IWorldSaveBatch
{
}

/// <summary>
/// Port for capturing and persisting the document projection of the live
/// simulation world. Implemented by the Marten-backed infrastructure layer.
/// </summary>
public interface IWorldPersistence
{
    IWorldSaveBatch? Build(World world, EmploymentIndex employmentIndex);

    Task SaveBatchAsync(IWorldSaveBatch batch);
}
