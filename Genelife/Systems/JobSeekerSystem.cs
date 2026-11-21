using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// System that automatically makes Sims job seekers when they reach working age.
///
/// Follows a collect -> apply pipeline so that no structural changes
/// (Create/Add/Remove/Destroy) happen while iterating a query.
/// </summary>
public class JobSeekerSystem : ISimulationSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private const int WorkingAge = 18;
    private readonly int cadenceInterval;

    public JobSeekerSystem(World world, int cadenceInterval = 100)
    {
        this.world = world;
        this.cadenceInterval = cadenceInterval;
        queryDescription = new QueryDescription()
            .WithAll<Alive>()
            .WithNone<Employee, JobSeeker>();
    }

    public string Name => "JobSeeker";
    public int Order => 600;
    public Cadence Cadence => new(cadenceInterval <= 0 ? 1 : cadenceInterval);

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        // 1. Collect (no structural changes during the query)
        var eligible = new List<Entity>();
        world.Query(in queryDescription, (Entity entity, ref Alive alive) =>
        {
            if (alive.Age >= WorkingAge)
            {
                eligible.Add(entity);
            }
        });

        // 2. Apply (structural changes only here, after the query completes)
        foreach (var entity in eligible)
        {
            if (!world.IsAlive(entity)) continue;

            var jobSeeker = new JobSeeker(
                isLookingForWork: true,
                skills: Enums.Skill.None,
                desiredSalary: 1000f
            );
            world.Add(entity, jobSeeker);
        }
    }
}
