using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that handles job applications and hiring process.
///
/// Follows a collect -> decide -> apply pipeline so that no structural
/// changes (Create/Add/Remove/Destroy) happen while iterating a query.
///
/// Open seats are represented by <see cref="OpenPosition"/> entities; hiring
/// destroys one seat per accepted applicant. The <see cref="EmploymentIndex"/>
/// is the sole derived view of employment relationships.
/// </summary>
public class HiringSystem
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly QueryDescription jobPostingsQuery;
    private readonly QueryDescription openPositionsQuery;
    private readonly QueryDescription jobSeekersQuery;
    private readonly QueryDescription applicationsQuery;
    private int skipCounter;
    private const int CadenceInterval = 25;

    public HiringSystem(World world, EmploymentIndex index)
    {
        this.world = world;
        this.index = index;
        jobPostingsQuery = new QueryDescription().WithAll<JobPosting>();
        openPositionsQuery = new QueryDescription().WithAll<OpenPosition>();
        jobSeekersQuery = new QueryDescription().WithAll<JobSeeker>().WithNone<Employee>();
        applicationsQuery = new QueryDescription().WithAll<JobApplication>();
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

        // 1. Collect
        var postings = CollectPostings();
        var openSeats = CollectOpenSeats();
        var seekers = CollectSeekers();
        var pendingApplicants = CollectPendingApplicants();

        // 2. Decide (pure C#, no world access)
        var newApplications = DecideApplications(postings, seekers, pendingApplicants);
        var hirePlan = DecideHires(postings, openSeats);

        // 3. Apply (structural changes only here)
        foreach (var application in newApplications)
        {
            world.Create(application);
        }

        foreach (var hire in hirePlan.Hires)
        {
            if (!world.IsAlive(hire.Applicant)) continue;
            HireApplicant(hire.Applicant, hire.Posting);
        }

        foreach (var appEntity in hirePlan.AcceptedApplications)
        {
            if (world.IsAlive(appEntity))
            {
                ref var app = ref world.Get<JobApplication>(appEntity);
                app.Status = ApplicationStatus.Accepted;
            }
        }

        foreach (var appEntity in hirePlan.RejectedApplications)
        {
            if (world.IsAlive(appEntity))
            {
                ref var app = ref world.Get<JobApplication>(appEntity);
                app.Status = ApplicationStatus.Rejected;
            }
        }

        foreach (var seatEntity in hirePlan.FilledSeats)
        {
            if (world.IsAlive(seatEntity))
            {
                world.Destroy(seatEntity);
            }
        }

        foreach (var postingEntity in hirePlan.FilledPostings)
        {
            if (world.IsAlive(postingEntity))
            {
                world.Destroy(postingEntity);
            }
        }
    }

    // ---- Collect ----

    private List<(Entity Entity, JobPosting Posting)> CollectPostings()
    {
        var postings = new List<(Entity Entity, JobPosting Posting)>();
        world.Query(in jobPostingsQuery, (Entity entity, ref JobPosting posting) =>
        {
            postings.Add((entity, posting));
        });
        return postings;
    }

    private Dictionary<Entity, List<Entity>> CollectOpenSeats()
    {
        var seats = new Dictionary<Entity, List<Entity>>();
        world.Query(in openPositionsQuery, (Entity entity, ref OpenPosition seat) =>
        {
            if (!seats.TryGetValue(seat.Posting, out var list))
            {
                list = new List<Entity>();
                seats[seat.Posting] = list;
            }
            list.Add(entity);
        });
        return seats;
    }

    private List<(Entity Entity, JobSeeker Seeker)> CollectSeekers()
    {
        var seekers = new List<(Entity Entity, JobSeeker Seeker)>();
        world.Query(in jobSeekersQuery, (Entity entity, ref JobSeeker seeker) =>
        {
            if (seeker.IsLookingForWork)
            {
                seekers.Add((entity, seeker));
            }
        });
        return seekers;
    }

    private HashSet<Entity> CollectPendingApplicants()
    {
        var applicants = new HashSet<Entity>();
        world.Query(in applicationsQuery, (ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Pending)
            {
                applicants.Add(app.Applicant);
            }
        });
        return applicants;
    }

    // ---- Decide ----

    private List<JobApplication> DecideApplications(
        List<(Entity Entity, JobPosting Posting)> postings,
        List<(Entity Entity, JobSeeker Seeker)> seekers,
        HashSet<Entity> pendingApplicants)
    {
        var newApplications = new List<JobApplication>();

        foreach (var (seekerEntity, seeker) in seekers)
        {
            // Skip seekers who already have a pending application
            if (pendingApplicants.Contains(seekerEntity)) continue;

            Entity bestJobEntity = default;
            JobPosting bestJob = default;
            float bestScore = 0f;

            foreach (var (jobEntity, jobPosting) in postings)
            {
                float score = CalculateMatchScore(seeker, jobPosting);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestJob = jobPosting;
                    bestJobEntity = jobEntity;
                }
            }

            if (bestScore > 0.3f)
            {
                newApplications.Add(new JobApplication(seekerEntity, bestJobEntity, bestScore));
            }
        }

        return newApplications;
    }

    private HirePlan DecideHires(
        List<(Entity Entity, JobPosting Posting)> postings,
        Dictionary<Entity, List<Entity>> openSeats)
    {
        var plan = new HirePlan();

        // Collect pending applications grouped by job posting
        var pending = new List<(Entity AppEntity, JobApplication Application)>();
        world.Query(in applicationsQuery, (Entity entity, ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Pending)
            {
                pending.Add((entity, app));
            }
        });

        var applicationsByJob = pending
            .GroupBy(a => a.Application.JobPosting)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Application.MatchScore).ToList());

        foreach (var (jobPostingEntity, applications) in applicationsByJob)
        {
            if (!world.IsAlive(jobPostingEntity)) continue;

            ref var jobPosting = ref world.Get<JobPosting>(jobPostingEntity);

            // Available seats for this posting
            if (!openSeats.TryGetValue(jobPostingEntity, out var seats))
            {
                seats = new List<Entity>();
            }

            int hired = 0;
            foreach (var (appEntity, application) in applications)
            {
                if (hired >= seats.Count) break;

                if (!world.IsAlive(application.Applicant)) continue;

                plan.Hires.Add((application.Applicant, jobPosting));
                plan.AcceptedApplications.Add(appEntity);
                plan.FilledSeats.Add(seats[hired]);
                hired++;
            }

            // Reject the remaining applications for this posting
            foreach (var (appEntity, _) in applications.Skip(hired))
            {
                plan.RejectedApplications.Add(appEntity);
            }

            // Mark posting for deletion if all seats are filled
            if (hired >= seats.Count)
            {
                plan.FilledPostings.Add(jobPostingEntity);
            }
        }

        return plan;
    }

    // ---- Apply ----

    private void HireApplicant(Entity applicantEntity, JobPosting jobPosting)
    {
        var employee = new Employee(
            jobPosting.Company,
            jobPosting.Office,
            jobPosting.Position,
            jobPosting.Salary
        );
        world.Add(applicantEntity, employee);

        if (!world.Has<Wallet>(applicantEntity))
        {
            world.Add(applicantEntity, new Wallet(0f));
        }

        if (world.Has<JobSeeker>(applicantEntity))
        {
            world.Remove<JobSeeker>(applicantEntity);
        }

        // Update the derived employment index
        index.Add(jobPosting.Company, applicantEntity);
    }

    private float CalculateMatchScore(JobSeeker seeker, JobPosting posting)
    {
        float score = 0f;

        // Skill match (40% weight)
        if (posting.Requirements.Count > 0)
        {
            int matchingSkills = seeker.Skills.Count(skill =>
                posting.Requirements.Any(req => req.Equals(skill, StringComparison.OrdinalIgnoreCase)));
            float skillMatch = (float)matchingSkills / posting.Requirements.Count;
            score += skillMatch * 0.4f;
        }
        else
        {
            score += 0.4f; // No requirements means anyone qualifies
        }

        // Salary match (60% weight)
        float salaryRatio = posting.Salary / Math.Max(seeker.DesiredSalary, 1f);
        float salaryScore = Math.Min(salaryRatio, 1f); // Cap at 1.0
        score += salaryScore * 0.6f;

        return score;
    }

    private sealed class HirePlan
    {
        public List<(Entity Applicant, JobPosting Posting)> Hires { get; } = new();
        public List<Entity> AcceptedApplications { get; } = new();
        public List<Entity> RejectedApplications { get; } = new();
        public List<Entity> FilledSeats { get; } = new();
        public List<Entity> FilledPostings { get; } = new();
    }
}
