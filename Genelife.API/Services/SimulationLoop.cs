using System.Diagnostics;

namespace Genelife.Api.Services;

public class SimulationLoop : BackgroundService
{
    private readonly SimulationEngine engine;
    private readonly ILogger<SimulationLoop>? logger;
    private const float FixedDeltaTime = 1f / 20f;
    private const int MaxCatchUpTicks = 5;

    private long tickCount;
    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(FixedDeltaTime));

    public SimulationLoop(SimulationEngine engine, ILogger<SimulationLoop>? logger = null)
    {
        this.engine = engine;
        this.logger = logger;
    }

    public long TickCount => Volatile.Read(ref tickCount);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var accumulator = TimeSpan.Zero;
        var sw = Stopwatch.StartNew();
        var lastTime = sw.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var currentTime = sw.Elapsed;
            var frameDelta = currentTime - lastTime;
            lastTime = currentTime;

            accumulator += frameDelta;

            var catchUpTicks = 0;
            while (accumulator >= TimeSpan.FromSeconds(FixedDeltaTime) && catchUpTicks < MaxCatchUpTicks)
            {
                engine.Tick(FixedDeltaTime);
                Interlocked.Increment(ref tickCount);
                accumulator -= TimeSpan.FromSeconds(FixedDeltaTime);
                catchUpTicks++;
            }

            if (catchUpTicks >= MaxCatchUpTicks)
            {
                logger?.LogWarning("Simulation loop max catch-up reached ({MaxCatchUpTicks} ticks), dropping {ExcessMs:F2}ms",
                    MaxCatchUpTicks, accumulator.TotalMilliseconds);
                accumulator = TimeSpan.Zero;
            }
        }
    }

    public double CurrentTickRatePerSecond()
    {
        return 1.0 / FixedDeltaTime;
    }
}