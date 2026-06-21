using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that handles job applications and hiring process
/// </summary>
public class HiringSystem
{
    private readonly World world;
    private readonly QueryDescription jobPostingsQuery;
    private readonly QueryDescription jobSeekersQuery;
    private readonly QueryDescription applicationsQuery;

    public HiringSystem(World world)
    {
        this.world = world;
        jobPostingsQuery = new QueryDescription().WithAll<JobPosting>();
        jobSeekersQuery = new QueryDescription().WithAll<JobSeeker>().WithNone<Employee>();
        applicationsQuery = new QueryDescription().WithAll<JobApplication>();
    }

    public void Update()
    {
        ProcessApplications();
        EvaluateAndHire();
    }

    /// <summary>
    /// Match job seekers with job postings and create applications
    /// </summary>
    private void ProcessApplications()
    {
        var jobPostings = new List<(Entity entity, JobPosting posting)>();
        world.Query(in jobPostingsQuery, (Entity entity, ref JobPosting posting) =>
        {
            if (posting.OpenPositions > 0)
            {
                jobPostings.Add((entity, posting));
            }
        });

        if (jobPostings.Count == 0) return;

        world.Query(in jobSeekersQuery, (Entity seekerEntity, ref JobSeeker seeker) =>
        {
            if (!seeker.IsLookingForWork) return;

            // Check if already applied to any jobs
            bool hasApplications = false;
            world.Query(in applicationsQuery, (ref JobApplication app) =>
            {
                if (app.ApplicantEntityId == seekerEntity.Id && app.Status == ApplicationStatus.Pending)
                {
                    hasApplications = true;
                }
            });

            if (hasApplications) return;

            // Find best matching job
            Entity bestJobEntity = default;
            JobPosting bestJob = default;
            float bestScore = 0f;
            
            foreach (var (jobEnt, jobPosting) in jobPostings)
            {
                float score = CalculateMatchScore(seeker, jobPosting);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestJob = jobPosting;
                    bestJobEntity = jobEnt;
                }
            }

            // Apply to best matching job if score is reasonable
            if (bestScore > 0.3f)
            {
                var application = new JobApplication(seekerEntity.Id, bestJobEntity.Id, bestScore);
                world.Create(application);
            }
        });
    }

    /// <summary>
    /// Evaluate pending applications and hire candidates
    /// </summary>
    private void EvaluateAndHire()
    {
        var applicationsToProcess = new List<(Entity entity, JobApplication application)>();
        
        world.Query(in applicationsQuery, (Entity entity, ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Pending)
            {
                applicationsToProcess.Add((entity, app));
            }
        });

        // Group applications by job posting
        var applicationsByJob = applicationsToProcess
            .GroupBy(a => a.application.JobPostingEntityId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.application.MatchScore).ToList());

        foreach (var (jobPostingId, applications) in applicationsByJob)
        {
            // Find the job posting entity
            Entity? jobEntity = null;
            world.Query(in jobPostingsQuery, (Entity entity, ref JobPosting posting) =>
            {
                if (entity.Id == jobPostingId)
                {
                    jobEntity = entity;
                }
            });
            
            if (jobEntity == null || !world.IsAlive(jobEntity.Value)) continue;
            
            ref var jobPosting = ref world.Get<JobPosting>(jobEntity.Value);
            
            int hired = 0;
            foreach (var (appEntity, application) in applications)
            {
                if (hired >= jobPosting.OpenPositions) break;

                // Find the applicant entity
                Entity? applicantEntity = null;
                world.Query(in jobSeekersQuery, (Entity entity, ref JobSeeker seeker) =>
                {
                    if (entity.Id == application.ApplicantEntityId)
                    {
                        applicantEntity = entity;
                    }
                });
                
                if (applicantEntity == null || !world.IsAlive(applicantEntity.Value)) continue;

                // Accept application
                ref var app = ref world.Get<JobApplication>(appEntity);
                app.Status = ApplicationStatus.Accepted;

                // Hire the applicant
                HireApplicant(applicantEntity.Value, ref jobPosting);
                hired++;
            }

            // Update open positions
            jobPosting.OpenPositions -= hired;

            // Reject remaining applications
            foreach (var (appEntity, application) in applications.Skip(hired))
            {
                if (world.IsAlive(appEntity))
                {
                    ref var app = ref world.Get<JobApplication>(appEntity);
                    app.Status = ApplicationStatus.Rejected;
                }
            }

            // Delete job posting if all positions filled
            if (jobPosting.OpenPositions <= 0)
            {
                world.Destroy(jobEntity.Value);
            }
        }
    }

    /// <summary>
    /// Hire an applicant by adding Employee component and removing JobSeeker
    /// </summary>
    private void HireApplicant(Entity applicantEntity, ref JobPosting jobPosting)
    {
        // Add Employee component
        var employee = new Employee(
            jobPosting.CompanyEntityId,
            jobPosting.OfficeEntityId,
            jobPosting.Position,
            jobPosting.Salary
        );
        world.Add(applicantEntity, employee);

        // Add Wallet if not present
        if (!world.Has<Wallet>(applicantEntity))
        {
            world.Add(applicantEntity, new Wallet(0f));
        }

        // Remove JobSeeker component
        if (world.Has<JobSeeker>(applicantEntity))
        {
            world.Remove<JobSeeker>(applicantEntity);
        }
    }

    /// <summary>
    /// Calculate match score between job seeker and job posting
    /// </summary>
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
}
