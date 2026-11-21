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
public class JobSeekerSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private const int WorkingAge = 18;
    private const int CadenceInterval = 100;
    private int skipCounter;

    public JobSeekerSystem(World world)
    {
        this.world = world;
        queryDescription = new QueryDescription()
            .WithAll<Alive>()
            .WithNone<Employee, JobSeeker>();
        skipCounter = 0;
    }

    public void Update()
    {
        if (skipCounter < CadenceInterval)
        {
            skipCounter++;
            return;
        }

        skipCounter = 0;

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
                skills: new List<string>(),
                desiredSalary: 1000f
            );
            world.Add(entity, jobSeeker);
        }
    }
}
