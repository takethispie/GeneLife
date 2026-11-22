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
using Genelife.Values;

namespace Genelife.Api.Repositories;

public class CompanyRepository
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;
    private readonly EconomyOptions options;

    public CompanyRepository(
        World world,
        EmploymentIndex index,
        NetworkIdRegistry networkIds,
        EconomyContext? economy = null,
        EconomyOptions? options = null)
    {
        this.world = world;
        this.index = index;
        this.networkIds = networkIds;
        this.economy = economy;
        this.options = options ?? economy?.Options ?? EconomyOptions.Default;
    }

    public NetworkId Add(string name, bool createHeadquarters = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));

        var entity = world.Create(new CompanyName(name));
        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);

        var capital = options.InitialCompanyCapital;
        if (economy is not null)
        {
            economy.Accounts.GetOrCreate(entity, capital, 0, "company.seed");
        }
        else
        {
            world.Add(entity, CompanyAccount.Create(capital, 0));
        }

        if (createHeadquarters)
        {
            var officeEntity = world.Create(new Office(
                "Headquarters",
                entity,
                10,
                options.DefaultOfficeOperatingCost));
            world.Add(officeEntity, networkIds.Allocate(officeEntity));
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

        if (world.Has<CompanyAccount>(entity))
        {
            ref var account = ref world.Get<CompanyAccount>(entity);
            status.Balance = account.Balance.Units;
            status.ReservedForPayroll = account.ReservedForPayroll.Units;
            status.Arrears = account.Arrears.Units;
            status.Solvency = account.State.ToString();
            status.RevenueLastCycle = account.RevenueLastCycle.Units;
            status.WagesLastCycle = account.WagesLastCycle.Units;
            status.OfficeCount = CountOffices(entity);
        }

        return status;
    }

    public List<CompanyStatus> GetAll()
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName, NetworkId>();

        world.Query(in query, (Entity entity, ref CompanyName companyName, ref NetworkId networkId) =>
        {
            var status = new CompanyStatus
            {
                NetworkId = networkId.Value,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity),
                OfficeCount = CountOffices(entity)
            };

            if (world.Has<CompanyAccount>(entity))
            {
                ref var account = ref world.Get<CompanyAccount>(entity);
                status.Balance = account.Balance.Units;
                status.ReservedForPayroll = account.ReservedForPayroll.Units;
                status.Arrears = account.Arrears.Units;
                status.Solvency = account.State.ToString();
                status.RevenueLastCycle = account.RevenueLastCycle.Units;
                status.WagesLastCycle = account.WagesLastCycle.Units;
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
                world.Destroy(office);
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

    private int CountOffices(Entity company)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<Office>();
        world.Query(in query, (ref Office office) =>
        {
            if (office.Company == company && !office.IsClosed)
                count++;
        });
        return count;
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
