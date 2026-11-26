using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Map;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.Api.Services;

/// <summary>
/// Yearly labour-market stabiliser. The population seed sizes the job stock for
/// the initial workforce; children ageing into work then grow the workforce
/// faster than any fixed stock. When unemployment exceeds the configured
/// trigger, this system creates a small number of government-funded companies
/// so the labour market can absorb the new workers instead of drifting into a
/// permanent unemployment pool.
/// </summary>
public sealed class JobGrowthSystem : ISimulationSystem
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext economy;
    private readonly EmploymentIndex employmentIndex;
    private readonly MapContext? map;

    public JobGrowthSystem(
        World world,
        NetworkIdRegistry networkIds,
        EconomyContext economy,
        EmploymentIndex employmentIndex,
        MapContext? map)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        this.employmentIndex = employmentIndex;
        this.map = map;
    }

    public string Name => "JobGrowth";
    public int Order => 545;
    public Cadence Cadence => new(economy.Clock.TicksPerYear);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var employed = 0;
        world.Query(in new QueryDescription().WithAll<Employee>().WithNone<Dead>(),
            (ref Employee _) => employed++);

        var unemployed = 0;
        world.Query(in new QueryDescription().WithAll<Lifecycle, JobSeeker>().WithNone<Employee, Dead>(),
            (ref Lifecycle lifecycle) =>
            {
                if (lifecycle.Stage == LifeStage.Work)
                    unemployed++;
            });

        var total = employed + unemployed;
        if (total == 0)
            return;

        var target = economy.Options.TargetUnemploymentRate;
        var trigger = target + economy.Options.JobGrowthTrigger;
        if (unemployed / (decimal)total <= trigger)
            return;

        var targetUnemployed = (int)Math.Floor(total * (double)target);
        var seatsNeeded = unemployed - targetUnemployed;
        if (seatsNeeded <= 0)
            return;

        var openSeats = 0;
        world.Query(in new QueryDescription().WithAll<JobPosting>(),
            (ref JobPosting posting) =>
            {
                if (!posting.IsClosed)
                    openSeats += posting.OpenSeats;
            });
        if (openSeats >= seatsNeeded)
            return;

        var seatsPerCompany = Math.Max(1, economy.Options.JobGrowthSeatsPerCompany);
        var capital = economy.Options.JobGrowthCompanyCapital;
        if (!capital.IsPositive)
            return;

        var companies = (int)Math.Ceiling((seatsNeeded - openSeats) / (double)seatsPerCompany);
        companies = Math.Min(companies, Math.Max(0, economy.Options.JobGrowthMaxPerYear));
        if (companies <= 0)
            return;

        var existingCompanies = 0;
        world.Query(in new QueryDescription().WithAll<CompanyName>(), (ref CompanyName _) => existingCompanies++);

        var companyRepository = new CompanyRepository(world, employmentIndex, networkIds, economy, map: map);
        var postingRepository = new JobPostingRepository(world, networkIds, economy);
        var skills = new[] { Skill.Sales, Skill.Programming, Skill.Engineering };

        for (var i = 0; i < companies; i++)
        {
            if (economy.Government.Balance < capital)
                break;

            var name = $"NewCo {existingCompanies + i + 1}";
            NetworkId companyId;
            try
            {
                companyId = companyRepository.Add(name, capital: Money.Zero);
            }
            catch (InvalidOperationException)
            {
                // No lot available for the headquarters. The repository has
                // already rolled back the partial company; stop growing this
                // year rather than faulting the tick loop.
                break;
            }
            economy.Emit(new CompanyCreated(companyId.Value, name));

            if (!networkIds.TryGetEntity(companyId, out var company))
                continue;

            economy.Government.Balance -= capital;
            var account = economy.Accounts.GetOrCreate(company, Money.Zero, tick, "jobs.company.fund");
            account.Balance += capital;
            account.State = CompanySolvency.Healthy;
            economy.Accounts.Set(company, account);
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.CapitalInjected,
                EconomyAccountIds.Government,
                companyId.Value,
                capital,
                "jobs.company.fund");

            var office = Entity.Null;
            world.Query(in new QueryDescription().WithAll<Office>(),
                (Entity entity, ref Office officeData) =>
                {
                    if (office == Entity.Null && officeData.Company == company)
                        office = entity;
                });
            if (office == Entity.Null)
                continue;

            var officeNetworkId = world.Get<NetworkId>(office);
            postingRepository.Open(
                companyId,
                officeNetworkId,
                i % 2 == 0 ? "Sales Associate" : "Engineer",
                Money.FromUnits(900 + (i % 2) * 150),
                skills[i % skills.Length],
                seatsPerCompany,
                tick);
        }
    }
}
