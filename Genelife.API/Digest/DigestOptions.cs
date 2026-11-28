namespace Genelife.Api.Digest;

/// <summary>
/// Configuration for the Town bell digest projection (GDD ch.08 section 8.5(6)).
/// Bound from the <c>Digest</c> configuration section.
/// </summary>
public sealed class DigestOptions
{
    public const string SectionName = "Digest";

    /// <summary>
    /// Length of the rolling window in simulation ticks. The digest is a rolling
    /// view (GDD: "an in-game day, or a session"), so this bounds how far back an
    /// event can be reported. Default is one in-game pay cycle (300 ticks); an
    /// in-game day is <c>VenueOptions.TicksPerDay</c> (24 ticks).
    /// </summary>
    public int WindowTicks { get; set; } = 300;

    /// <summary>
    /// Maximum number of events retained in memory. The buffer is bounded and
    /// drops the oldest events first, so the projection can never grow without
    /// limit. No new persistence is introduced — the buffer lives in process
    /// memory and is rebuilt from the event channel after a restart.
    /// </summary>
    public int BufferCapacity { get; set; } = 100_000;
}
