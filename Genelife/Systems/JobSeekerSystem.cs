using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// System that automatically makes Sims job seekers when they reach working age
/// </summary>
public class JobSeekerSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private const int WorkingAge = 18;

    public JobSeekerSystem(World world)
    {
        this.world = world;
        // Query Sims with Alive component but without Employee or JobSeeker components
        queryDescription = new QueryDescription()
            .WithAll<Alive>()
            .WithNone<Employee, JobSeeker>();
    }

    public void Update()
    {
        world.Query(in queryDescription, (Entity entity, ref Alive alive) =>
        {
            // If Sim is 18 or older and not employed, make them a job seeker
            if (alive.Age >= WorkingAge)
            {
                var jobSeeker = new JobSeeker(
                    isLookingForWork: true,
                    skills: new List<string>(),
                    desiredSalary: 1000f
                );
                world.Add(entity, jobSeeker);
            }
        });
    }
}
