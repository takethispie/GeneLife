using System.Diagnostics;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.Api.Repositories;

public class CompanyRepository
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;
    private readonly EconomyOptions options;
    private readonly MapContext? map;

    public CompanyRepository(
        World world,
        EmploymentIndex index,
        NetworkIdRegistry networkIds,
        EconomyContext? economy = null,
        EconomyOptions? options = null,
        MapContext? map = null)
    {
        this.world = world;
        this.index = index;
        this.networkIds = networkIds;
        this.economy = economy;
        this.options = options ?? economy?.Options ?? EconomyOptions.Default;
        this.map = map;
    }

    public NetworkId Add(string name, bool createHeadquarters = true, Money? capital = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));

        var entity = world.Create(new CompanyName(name));
        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);

        var initialCapital = capital ?? options.InitialCompanyCapital;
        if (economy is not null)
        {
            economy.Accounts.GetOrCreate(entity, initialCapital, 0, "company.seed");
        }
        else
        {
            world.Add(entity, CompanyAccount.Create(initialCapital, 0));
        }

        if (createHeadquarters)
        {
            var officeEntity = world.Create(new Office(
                "Headquarters",
                entity,
                10,
                options.DefaultOfficeOperatingCost));
            var officeId = networkIds.Allocate(officeEntity);
            world.Add(officeEntity, officeId);

            if (map is not null)
            {
                var placement = map.Placement.TryPlace(
                    officeEntity,
                    officeId,
                    BuildingKind.Office,
                    BuildingFootprints.ForOffice(10),
                    "Central");
                if (!placement.Success)
                {
                    networkIds.Remove(officeEntity);
                    world.Destroy(officeEntity);
                    networkIds.Remove(entity);
                    world.Destroy(entity);
                    throw new InvalidOperationException(
                        $"Unable to place headquarters: {placement.Error} {placement.Message}");
                }
            }
        }

        return networkId;
    }

    public CompanyStatus? Get(NetworkId networkId)
    {
        if (!networkIds.TryGetEntity(networkId, out var entity))
            return null;

        if (!world.IsAlive(entity) || !world.Has<CompanyName>(entity))
            return null;

        ref var companyName = ref world.Get<CompanyName>(entity);
        var status = new CompanyStatus
        {
            NetworkId = networkId.Value,
            Name = companyName.Name,
            EmployeeCount = index.CountEmployees(entity)
        };

        status.OfficeCount = CompanyOfficeCounter.CountOpenByCompany(world).GetValueOrDefault(entity);

        if (world.Has<CompanyAccount>(entity))
        {
            var financials = CompanyFinancialSnapshot.From(world.Get<CompanyAccount>(entity));
            status.Balance = financials.Balance;
            status.ReservedForPayroll = financials.ReservedForPayroll;
            status.Arrears = financials.Arrears;
            status.Solvency = financials.Solvency;
            status.RevenueLastCycle = financials.RevenueLastCycle;
            status.WagesLastCycle = financials.WagesLastCycle;
        }

        return status;
    }

    public List<CompanyStatus> GetAll()
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName, NetworkId>();
        var officesByCompany = CompanyOfficeCounter.CountOpenByCompany(world);

        world.Query(in query, (Entity entity, ref CompanyName companyName, ref NetworkId networkId) =>
        {
            var status = new CompanyStatus
            {
                NetworkId = networkId.Value,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity),
                OfficeCount = officesByCompany.GetValueOrDefault(entity)
            };

            if (world.Has<CompanyAccount>(entity))
            {
                var financials = CompanyFinancialSnapshot.From(world.Get<CompanyAccount>(entity));
                status.Balance = financials.Balance;
                status.ReservedForPayroll = financials.ReservedForPayroll;
                status.Arrears = financials.Arrears;
                status.Solvency = financials.Solvency;
                status.RevenueLastCycle = financials.RevenueLastCycle;
                status.WagesLastCycle = financials.WagesLastCycle;
            }

            companies.Add(status);
        });

        return companies;
    }

    public bool InjectCapital(NetworkId networkId, Money amount, long tick = 0)
    {
        if (amount.IsNegative || amount.IsZero)
            return false;
        if (!networkIds.TryGetEntity(networkId, out var entity) || !world.IsAlive(entity))
            return false;

        if (economy is not null)
        {
            economy.Accounts.Credit(
                entity,
                amount,
                LedgerKind.CapitalInjected,
                Guid.NewGuid(),
                null,
                "capital.injection",
                tick);
        }
        else
        {
            var account = world.Has<CompanyAccount>(entity)
                ? world.Get<CompanyAccount>(entity)
                : CompanyAccount.Create(Money.Zero, tick);
            account.Balance += amount;
            if (world.Has<CompanyAccount>(entity))
                world.Get<CompanyAccount>(entity) = account;
            else
                world.Add(entity, account);
        }

        economy?.Emit(new Genelife.Messages.Events.CapitalInjected(
            networkId.Value,
            amount.Cents,
            tick));
        return true;
    }

    public bool Delete(NetworkId networkId)
    {
        if (!networkIds.TryGetEntity(networkId, out var target))
            return false;

        if (!world.IsAlive(target) || !world.Has<CompanyName>(target))
            return false;

        if (index.EmployeesByCompany.TryGetValue(target, out var employees))
        {
            foreach (var employeeEntity in employees)
            {
                if (world.IsAlive(employeeEntity) && world.Has<Employee>(employeeEntity))
                    world.Remove<Employee>(employeeEntity);
            }
        }

        index.RemoveByCompany(target);

        // Remove postings and their seats, offices, and remaining applications.
        var postings = new List<Entity>();
        var postingQuery = new QueryDescription().WithAll<JobPosting>();
        world.Query(in postingQuery, (Entity entity, ref JobPosting posting) =>
        {
            if (posting.Company == target)
                postings.Add(entity);
        });

        var seats = new List<Entity>();
        var seatQuery = new QueryDescription().WithAll<OpenPosition>();
        world.Query(in seatQuery, (Entity entity, ref OpenPosition seat) =>
        {
            if (postings.Contains(seat.Posting))
                seats.Add(entity);
        });
        foreach (var seat in seats)
        {
            if (world.IsAlive(seat))
                world.Destroy(seat);
        }
        foreach (var posting in postings)
        {
            if (world.IsAlive(posting))
                world.Destroy(posting);
        }

        var offices = new List<Entity>();
        var officeQuery = new QueryDescription().WithAll<Office>();
        world.Query(in officeQuery, (Entity entity, ref Office office) =>
        {
            if (office.Company == target)
                offices.Add(entity);
        });
        foreach (var office in offices)
        {
            if (world.IsAlive(office))
            {
                if (map is not null && networkIds.TryGetNetworkId(office, out var officeId))
                    map.Placement.Remove(officeId);
                world.Destroy(office);
            }
        }

        var applications = new List<Entity>();
        var appQuery = new QueryDescription().WithAll<JobApplication>();
        world.Query(in appQuery, (Entity entity, ref JobApplication app) =>
        {
            if (app.Company == target)
                applications.Add(entity);
        });
        foreach (var app in applications)
        {
            if (world.IsAlive(app))
                world.Destroy(app);
        }

        if (economy is not null && world.Has<CompanyAccount>(target))
        {
            var account = world.Get<CompanyAccount>(target);
            if (account.Balance.IsPositive)
            {
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    0,
                    LedgerKind.WriteOff,
                    networkId.Value,
                    EconomyAccountIds.Government,
                    account.Balance,
                    "company.delete.residual");
                economy.Government.Balance += account.Balance;
                account.Balance = Money.Zero;
                world.Get<CompanyAccount>(target) = account;
            }
            else if (account.Balance.IsNegative)
            {
                account.Balance = Money.Zero;
                world.Get<CompanyAccount>(target) = account;
            }
        }

        world.Destroy(target);
        networkIds.Remove(networkId);

        DebugInvariantCheck();
        return true;
    }

    [Conditional("DEBUG")]
    private void DebugInvariantCheck()
    {
        var query = new QueryDescription().WithAll<Employee>();
        int ecsCount = 0;
        world.Query(in query, (ref Employee _) => ecsCount++);

        if (ecsCount != index.TotalEmployees())
        {
            Debug.Fail($"Employment invariant broken: ECS has {ecsCount} Employee components, EmploymentIndex tracks {index.TotalEmployees()}");
        }
    }

    public bool DebugInvariantPasses()
    {
        var query = new QueryDescription().WithAll<Employee>();
        int ecsCount = 0;
        world.Query(in query, (ref Employee _) => ecsCount++);
        return ecsCount == index.TotalEmployees();
    }
}
