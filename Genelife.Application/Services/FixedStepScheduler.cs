namespace Genelife.Api.Services;

/// <summary>
/// Fixed-step accumulator with bounded catch-up. Frame time is accumulated and
/// converted into whole fixed steps; when the backlog exceeds the catch-up cap
/// the excess is discarded instead of growing without bound.
/// </summary>
public sealed class FixedStepScheduler
{
    private readonly TimeSpan step;
    private readonly int maxCatchUp;
    private TimeSpan accumulator;

    public FixedStepScheduler(TimeSpan step, int maxCatchUp)
    {
        if (step <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
        if (maxCatchUp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCatchUp), "Max catch-up must be positive.");

        this.step = step;
        this.maxCatchUp = maxCatchUp;
    }

    public TimeSpan Accumulator => accumulator;

    /// <summary>
    /// Adds <paramref name="frameDelta"/> and returns how many fixed steps should
    /// run now. Any backlog beyond <see cref="MaxCatchUp"/> is reported through
    /// <paramref name="discarded"/> and dropped.
    /// </summary>
    public int Advance(TimeSpan frameDelta, out TimeSpan discarded)
    {
        if (frameDelta < TimeSpan.Zero)
            frameDelta = TimeSpan.Zero;

        accumulator += frameDelta;

        var steps = 0;
        while (accumulator >= step && steps < maxCatchUp)
        {
            accumulator -= step;
            steps++;
        }

        if (accumulator >= step)
        {
            discarded = accumulator;
            accumulator = TimeSpan.Zero;
        }
        else
        {
            discarded = TimeSpan.Zero;
        }

        return steps;
    }
}
