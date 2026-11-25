using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Safety net that makes a Work-stage sim a job seeker when they have neither an
/// employee role nor an active search. Stage progression usually adds the seeker
/// directly at graduation; this system exists to recover from restored or
/// manually modified worlds.
///
/// Follows a collect -> apply pipeline so no structural changes happen while
/// iterating a query.
/// </summary>
public class JobSeekerSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext? economy;
    private readonly QueryDescription queryDescription;
    private readonly int cadenceInterval;

    public JobSeekerSystem(World world, int cadenceInterval = 100, EconomyContext? economy = null)
    {
        this.world = world;
        this.economy = economy;
        this.cadenceInterval = cadenceInterval;
        queryDescription = new QueryDescription()
            .WithAll<Alive>()
            .WithNone<Employee, JobSeeker, Dead, PlayerControlled>();
    }

    public string Name => "JobSeeker";
    public int Order => 600;
    public Cadence Cadence => new(cadenceInterval <= 0 ? 1 : cadenceInterval);

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        var eligible = new List<Entity>();
        world.Query(in queryDescription, (Entity entity, ref Alive alive) =>
        {
            if (world.Has<Lifecycle>(entity))
            {
                if (world.Get<Lifecycle>(entity).Stage == LifeStage.Work)
                    eligible.Add(entity);
                return;
            }

            // Legacy sims without lifecycle state keep the old age fallback.
            var policy = economy?.Life.Policy ?? LifeStagePolicy.Default;
            if (alive.Age >= policy.WorkStartAge && alive.Age < policy.RetirementAge)
                eligible.Add(entity);
        });

        foreach (var entity in eligible)
        {
            if (!world.IsAlive(entity))
                continue;

            var profile = world.Has<SkillProfile>(entity) ? world.Get<SkillProfile>(entity) : new SkillProfile();
            var careers = economy?.Life.Careers ?? CareerPolicy.Default;
            var salary = profile.Total > 0f ? careers.SalaryForSkills(profile.Total) : Money.FromUnits(1_000m);

            world.Add(entity, new JobSeeker(true, profile.ToMask(), salary));
        }
    }
}
