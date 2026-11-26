using Genelife.Economy;
using Genelife.Application.Systems;

using Genelife.Family;
namespace Genelife.Application.Systems;

public sealed class TrustSystem : ISimulationSystem
{
    private readonly EconomyContext economy;
    private readonly TrustService trusts;

    private readonly int cadenceOffset;

    public TrustSystem(EconomyContext economy, TrustService trusts, int cadenceOffset = 0)
    {
        this.economy = economy;
        this.trusts = trusts;
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "Trust";
    public int Order => 386;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick) => trusts.ReleaseDue(tick);
}
