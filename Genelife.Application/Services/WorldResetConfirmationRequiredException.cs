namespace Genelife.Api.Services;

/// <summary>
/// Raised when a world reset is requested without confirmation while a map
/// already exists. The API maps this to a 409 so the UI can prompt.
/// </summary>
public sealed class WorldResetConfirmationRequiredException : InvalidOperationException
{
    public WorldResetConfirmationRequiredException()
        : base("A map already exists; confirmation is required.")
    {
    }
}
