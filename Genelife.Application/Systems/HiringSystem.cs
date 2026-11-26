using Arch.Core;
using Arch.Core.Extensions;
using System.Numerics;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Job application and hiring pipeline. Hiring charges a recruitment fee,
/// respects company solvency and freezes, and updates the EmploymentIndex.
///
/// Follows collect -> decide -> apply so no structural changes happen while
/// iterating a query.
/// </summary>
public class HiringSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly EconomyContext? economy;
    private readonly QueryDescription jobPostingsQuery;
    private readonly QueryDescription openPositionsQuery;
    private readonly QueryDescription jobSeekersQuery;
    private readonly QueryDescription applicationsQuery;
    private readonly int cadenceInterval;

    public HiringSystem(World world, EmploymentIndex index, int cadenceInterval = 25, EconomyContext? economy = null)
    {
        this.world = world;
        this.index = index;
        this.economy = economy;
        this.cadenceInterval = cadenceInterval;
        jobPostingsQuery = new QueryDescription().WithAll<JobPosting>();
        openPositionsQuery = new QueryDescription().WithAll<OpenPosition>();
        jobSeekersQuery = new QueryDescription().WithAll<JobSeeker>().WithNone<Employee, Components.Gen.Dead, Components.Gen.PlayerControlled>();
        applicationsQuery = new QueryDescription().WithAll<JobApplication>();
    }

    public string Name => "Hiring";
    public int Order => 400;
    public Cadence Cadence => new(cadenceInterval <= 0 ? 1 : cadenceInterval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update() => Update(0);

    public void Update(long tick)
    {
        var postings = CollectPostings();
        var openSeats = CollectOpenSeats();
        var seekers = CollectSeekers();
        var pendingApplicants = CollectPendingApplicants();

        var newApplications = DecideApplications(postings, seekers, pendingApplicants);
        var hirePlan = DecideHires(postings, openSeats);

        foreach (var application in newApplications)
            world.Create(application);

        foreach (var hire in hirePlan.Hires)
        {
            if (!world.IsAlive(hire.Applicant) || !world.IsAlive(hire.PostingEntity))
            {
                Reject(hire.ApplicationEntity);
                continue;
            }

            if (!TryHire(tick, hire))
            {
                Reject(hire.ApplicationEntity);
                continue;
            }

            Accept(hire.ApplicationEntity);

            if (world.IsAlive(hire.SeatEntity))
                world.Destroy(hire.SeatEntity);

            ref var posting = ref world.Get<JobPosting>(hire.PostingEntity);
            posting.OpenSeats = Math.Max(0, posting.OpenSeats - 1);
            if (posting.OpenSeats == 0)
            {
                var postingNetworkId = world.Has<NetworkId>(hire.PostingEntity)
                    ? world.Get<NetworkId>(hire.PostingEntity).Value
                    : 0UL;
                var companyNetworkId = CompanyAccountService.NetworkIdOf(world, posting.Company);
                posting.IsClosed = true;
                economy?.Emit(new JobPostingClosed(postingNetworkId, companyNetworkId));
            }
        }

        foreach (var appEntity in hirePlan.RejectedApplications)
            Reject(appEntity);
    }

    private List<(Entity Entity, JobPosting Posting)> CollectPostings()
    {
        var postings = new List<(Entity Entity, JobPosting Posting)>();
        world.Query(in jobPostingsQuery, (Entity entity, ref JobPosting posting) =>
        {
            if (!posting.IsClosed)
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
                seekers.Add((entity, seeker));
        });
        return seekers;
    }

    private HashSet<Entity> CollectPendingApplicants()
    {
        var applicants = new HashSet<Entity>();
        world.Query(in applicationsQuery, (ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Pending)
                applicants.Add(app.Applicant);
        });
        return applicants;
    }

    private List<JobApplication> DecideApplications(
        List<(Entity Entity, JobPosting Posting)> postings,
        List<(Entity Entity, JobSeeker Seeker)> seekers,
        HashSet<Entity> pendingApplicants)
    {
        var newApplications = new List<JobApplication>();

        foreach (var (seekerEntity, seeker) in seekers)
        {
            if (pendingApplicants.Contains(seekerEntity))
                continue;

            Entity bestJobEntity = default;
            JobPosting bestJob = default;
            float bestScore = 0f;

            foreach (var (jobEntity, jobPosting) in postings)
            {
                var score = CalculateMatchScore(seekerEntity, seeker, jobPosting);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestJob = jobPosting;
                    bestJobEntity = jobEntity;
                }
            }

            if (bestScore > 0.3f)
            {
                newApplications.Add(new JobApplication(
                    seekerEntity,
                    bestJobEntity,
                    bestJob.Position ?? string.Empty,
                    bestJob.Company,
                    bestScore));
            }
        }

        return newApplications;
    }

    private HirePlan DecideHires(
        List<(Entity Entity, JobPosting Posting)> postings,
        Dictionary<Entity, List<Entity>> openSeats)
    {
        var plan = new HirePlan();

        var pending = new List<(Entity AppEntity, JobApplication Application)>();
        world.Query(in applicationsQuery, (Entity entity, ref JobApplication app) =>
        {
            if (app.Status == ApplicationStatus.Pending)
                pending.Add((entity, app));
        });

        var applicationsByJob = pending
            .GroupBy(a => a.Application.JobPosting)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Application.MatchScore).ToList());

        foreach (var (jobPostingEntity, applications) in applicationsByJob)
        {
            if (!world.IsAlive(jobPostingEntity))
                continue;

            ref var jobPosting = ref world.Get<JobPosting>(jobPostingEntity);
            if (jobPosting.IsClosed)
            {
                foreach (var (appEntity, _) in applications)
                    plan.RejectedApplications.Add(appEntity);
                continue;
            }

            if (!openSeats.TryGetValue(jobPostingEntity, out var seats))
                seats = new List<Entity>();

            var hired = 0;
            foreach (var (appEntity, application) in applications)
            {
                if (hired >= seats.Count)
                    break;
                if (!world.IsAlive(application.Applicant))
                    continue;

                plan.Hires.Add(new HireDecision(
                    application.Applicant,
                    jobPostingEntity,
                    jobPosting,
                    seats[hired],
                    appEntity));
                hired++;
            }

            foreach (var (appEntity, _) in applications.Skip(hired))
                plan.RejectedApplications.Add(appEntity);
        }

        return plan;
    }

    private bool TryHire(long tick, HireDecision hire)
    {
        if (economy is not null)
        {
            if (!economy.Accounts.TryGet(hire.Posting.Company, out var account))
                return false;

            if (account.State is CompanySolvency.Insolvent or CompanySolvency.Bankrupt ||
                account.HiringFrozen ||
                account.Arrears.IsPositive)
                return false;

            var fee = (hire.Posting.SalaryPerCycle * economy.Options.RecruitmentRate)
                .Max(Money.FromUnits(1m));

            if (!economy.Accounts.TryDebit(
                    hire.Posting.Company,
                    fee,
                    LedgerKind.RecruitmentCost,
                    Guid.NewGuid(),
                    EconomyAccountIds.Government,
                    "recruitment.fee",
                    tick))
                return false;

            economy.Government.Balance += fee;
            economy.Emit(new RecruitmentFeeCharged(
                CompanyAccountService.NetworkIdOf(world, hire.Posting.Company),
                CompanyAccountService.NetworkIdOf(world, hire.Applicant),
                fee.Cents,
                tick));
        }

        var employee = new Employee(
            hire.Posting.Company,
            hire.Posting.Office,
            hire.Posting.Position,
            hire.Posting.SalaryPerCycle);

        employee.HiredAtTick = tick;
        world.Add(hire.Applicant, employee);

        if (!world.Has<Wallet>(hire.Applicant))
            world.Add(hire.Applicant, new Wallet(Money.Zero));

        if (world.Has<JobSeeker>(hire.Applicant))
            world.Remove<JobSeeker>(hire.Applicant);

        if (!world.Has<EmploymentRecord>(hire.Applicant))
            world.Add(hire.Applicant, new EmploymentRecord(tick));
        if (!world.Has<WorkSchedule>(hire.Applicant))
            world.Add(hire.Applicant, new WorkSchedule(hire.Posting.Office));

        if (world.Has<UnemploymentRecord>(hire.Applicant))
        {
            world.Remove<UnemploymentRecord>(hire.Applicant);
            economy?.Emit(new UnemploymentEnded(
                CompanyAccountService.NetworkIdOf(world, hire.Applicant),
                "hired",
                tick));
        }

        index.Add(hire.Posting.Company, hire.Applicant);

        economy?.Emit(new EmployeeHired(
            CompanyAccountService.NetworkIdOf(world, hire.Posting.Company),
            CompanyAccountService.NetworkIdOf(world, hire.Applicant),
            hire.Posting.Position,
            hire.Posting.SalaryPerCycle.Cents));

        return true;
    }

    /// <summary>
    /// Hires one explicitly chosen applicant (a player accepting an offer)
    /// through the same seat, solvency, freeze, fee and index path as the NPC
    /// hiring pass.
    /// </summary>
    public bool TryHireApplicant(Entity applicant, Entity postingEntity, long tick, out string? error)
    {
        error = null;

        if (!world.IsAlive(applicant) || world.Has<Components.Gen.Dead>(applicant))
        {
            error = "That genos is not available.";
            return false;
        }
        if (world.Has<Employee>(applicant))
        {
            error = "That genos is already employed.";
            return false;
        }
        if (!world.IsAlive(postingEntity) || !world.Has<JobPosting>(postingEntity))
        {
            error = "That posting no longer exists.";
            return false;
        }

        var posting = world.Get<JobPosting>(postingEntity);
        if (posting.IsClosed || posting.OpenSeats <= 0)
        {
            error = "That posting is no longer open.";
            return false;
        }

        var seat = Entity.Null;
        world.Query(in openPositionsQuery, (Entity entity, ref OpenPosition open) =>
        {
            if (seat == Entity.Null && open.Posting == postingEntity)
                seat = entity;
        });
        if (seat == Entity.Null)
        {
            error = "There are no open seats for that posting.";
            return false;
        }

        var hire = new HireDecision(applicant, postingEntity, posting, seat, Entity.Null);
        if (!TryHire(tick, hire))
        {
            error = "The company could not hire right now.";
            return false;
        }

        if (world.IsAlive(seat))
            world.Destroy(seat);

        posting = world.Get<JobPosting>(postingEntity);
        posting.OpenSeats = Math.Max(0, posting.OpenSeats - 1);
        if (posting.OpenSeats == 0)
        {
            posting.IsClosed = true;
            var postingNetworkId = world.Has<NetworkId>(postingEntity)
                ? world.Get<NetworkId>(postingEntity).Value
                : 0UL;
            economy?.Emit(new JobPostingClosed(
                postingNetworkId,
                CompanyAccountService.NetworkIdOf(world, posting.Company)));
        }
        world.Get<JobPosting>(postingEntity) = posting;
        return true;
    }

    private void Accept(Entity appEntity)
    {
        if (world.IsAlive(appEntity))
            world.Get<JobApplication>(appEntity).Status = ApplicationStatus.Accepted;
    }
    private void Reject(Entity appEntity)
    {
        if (world.IsAlive(appEntity))
            world.Get<JobApplication>(appEntity).Status = ApplicationStatus.Rejected;
    }

    private float CalculateMatchScore(Entity seekerEntity, JobSeeker seeker, JobPosting posting)
    {
        var score = 0f;

        if (posting.Requirements != Skill.None)
        {
            var required = BitOperations.PopCount((uint)posting.Requirements);
            var matchingSkills = BitOperations.PopCount((uint)(posting.Requirements & seeker.Skills));
            var skillMatch = required > 0 ? (float)matchingSkills / required : 1f;

            if (world.Has<SkillProfile>(seekerEntity))
            {
                var profile = world.Get<SkillProfile>(seekerEntity);
                var proficiency = RequiredProficiency(profile, posting.Requirements);
                score += skillMatch * 0.25f + Math.Clamp(proficiency, 0f, 1f) * 0.15f;
            }
            else
            {
                score += skillMatch * 0.4f;
            }
        }
        else
        {
            score += 0.4f;
        }

        var desired = Math.Max(seeker.DesiredSalary.Cents, 1L);
        var salaryRatio = posting.SalaryPerCycle.Cents / (float)desired;
        score += Math.Min(salaryRatio, 1f) * 0.6f;

        return score;
    }

    private static float RequiredProficiency(SkillProfile profile, Skill requirements)
    {
        var total = 0f;
        var count = 0;
        foreach (var family in SkillProfile.Families)
        {
            if (!requirements.HasFlag(family))
                continue;
            total += profile.Get(family);
            count++;
        }
        return count > 0 ? total / count : 0f;
    }

    private sealed class HirePlan
    {
        public List<HireDecision> Hires { get; } = new();
        public List<Entity> RejectedApplications { get; } = new();
    }

    private readonly record struct HireDecision(
        Entity Applicant,
        Entity PostingEntity,
        JobPosting Posting,
        Entity SeatEntity,
        Entity ApplicationEntity);
}
