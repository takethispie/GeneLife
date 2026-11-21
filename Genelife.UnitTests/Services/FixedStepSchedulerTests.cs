using Genelife.Api.Services;

namespace Genelife.UnitTests.Services;

public class FixedStepSchedulerTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    [Fact]
    public void Advance_ConvertsFrameTimeIntoWholeSteps()
    {
        var scheduler = new FixedStepScheduler(Step, maxCatchUp: 5);

        var steps = scheduler.Advance(TimeSpan.FromMilliseconds(35), out var discarded);

        Assert.Equal(3, steps);
        Assert.Equal(TimeSpan.FromMilliseconds(5), scheduler.Accumulator);
        Assert.Equal(TimeSpan.Zero, discarded);
    }

    [Fact]
    public void Advance_CarriesPartialStepIntoNextFrame()
    {
        var scheduler = new FixedStepScheduler(Step, maxCatchUp: 5);

        Assert.Equal(1, scheduler.Advance(TimeSpan.FromMilliseconds(15), out _));
        Assert.Equal(1, scheduler.Advance(TimeSpan.FromMilliseconds(5), out _));
    }

    [Fact]
    public void Advance_CapsCatchUpAndDropsExcessBacklog()
    {
        var scheduler = new FixedStepScheduler(Step, maxCatchUp: 3);

        var steps = scheduler.Advance(TimeSpan.FromMilliseconds(100), out var discarded);

        Assert.Equal(3, steps);
        Assert.Equal(TimeSpan.FromMilliseconds(70), discarded);
        Assert.Equal(TimeSpan.Zero, scheduler.Accumulator);
    }

    [Fact]
    public void Advance_TreatsNegativeFrameTimeAsZero()
    {
        var scheduler = new FixedStepScheduler(Step, maxCatchUp: 3);

        var steps = scheduler.Advance(TimeSpan.FromMilliseconds(-50), out var discarded);

        Assert.Equal(0, steps);
        Assert.Equal(TimeSpan.Zero, discarded);
        Assert.Equal(TimeSpan.Zero, scheduler.Accumulator);
    }

    [Fact]
    public void Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepScheduler(TimeSpan.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepScheduler(Step, 0));
    }
}
