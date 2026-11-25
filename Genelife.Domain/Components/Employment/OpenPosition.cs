using Arch.Core;

namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a single open seat for a job posting.
/// One entity per seat; destroyed when the seat is filled.
/// </summary>
public record struct OpenPosition
{
    public Entity Posting;

    public OpenPosition(Entity posting)
    {
        Posting = posting;
    }
}