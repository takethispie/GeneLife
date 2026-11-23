using Genelife.Economy;
using Genelife.Systems;

namespace Genelife.Family;

public sealed class TrustSystem : ISimulationSystem
{
    private readonly EconomyContext economy;
    private readonly TrustService trusts;

    public TrustSystem(EconomyContext economy, TrustService trusts)
    {
        this.economy = economy;
        this.trusts = trusts;
    }

    public string Name => "Trust";
    public int Order => 386;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick) => trusts.ReleaseDue(tick);
}
