using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components;

namespace Genelife.Systems;

/// <summary>
/// System that automatically makes Sims job seekers when they reach working age
/// </summary>
public class JobSeekerSystem
{
    private readonly World _world;
    private readonly QueryDescription _queryDescription;
    private const int WorkingAge = 18;

    public JobSeekerSystem(World world)
    {
        _world = world;
        // Query Sims with Alive component but without Employee or JobSeeker components
        _queryDescription = new QueryDescription()
            .WithAll<Alive>()
            .WithNone<Employee, JobSeeker>();
    }

    public void Update()
    {
        _world.Query(in _queryDescription, (Entity entity, ref Alive alive) =>
        {
            // If Sim is 18 or older and not employed, make them a job seeker
            if (alive.Age >= WorkingAge)
            {
                var jobSeeker = new JobSeeker(
                    isLookingForWork: true,
                    skills: new List<string>(),
                    desiredSalary: 1000f
                );
                _world.Add(entity, jobSeeker);
            }
        });
    }
}
