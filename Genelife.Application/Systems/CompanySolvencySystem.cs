using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Solvency state machine and liquidation waterfall. Distress is visible for a
/// grace period; restructuring cuts costs before the company is liquidated.
/// Liquidation never deletes the company entity, so history stays available.
/// </summary>
public class CompanySolvencySystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public CompanySolvencySystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
    }

    public string Name => "CompanySolvency";
    public int Order => 540;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var companies = new List<(Entity Entity, CompanyAccount Account)>();
        var query = new QueryDescription().WithAll<CompanyName, CompanyAccount>();
        world.Query(in query, (Entity entity, ref CompanyName _, ref CompanyAccount account) =>
            companies.Add((entity, account)));

        foreach (var (company, account) in companies)
        {
            if (!world.IsAlive(company) || account.State == CompanySolvency.Bankrupt)
                continue;

            Evaluate(tick, company, account);
        }
    }

    private void Evaluate(long tick, Entity company, CompanyAccount account)
    {
        if (HasDefaultedLoan(company))
            account.State = CompanySolvency.Insolvent;

        if (account.Arrears.IsPositive)
        {
            account.ArrearsCycles = Math.Max(account.ArrearsCycles, 1);
            if (account.ArrearsCycles >= economy.Options.InsolvencyGraceCycles)
                account.State = CompanySolvency.Insolvent;
        }
        else
        {
            account.ArrearsCycles = 0;
        }

        var payrollReserve = account.ReservedForPayroll;
        switch (account.State)
        {
            case CompanySolvency.Healthy:
                if (account.Balance < payrollReserve)
                    Transition(ref account, CompanySolvency.Tight, tick);
                break;

            case CompanySolvency.Tight:
                if (account.Balance >= payrollReserve && !account.Arrears.IsPositive)
                    Transition(ref account, CompanySolvency.Healthy, tick);
                break;

            case CompanySolvency.Arrears:
                if (!account.Arrears.IsPositive)
                    Transition(ref account, account.Balance >= payrollReserve
                        ? CompanySolvency.Healthy
                        : CompanySolvency.Tight, tick);
                break;

            case CompanySolvency.Insolvent:
                account.InsolventCycles++;
                Restructure(tick, company);
                account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);

                if (!account.Arrears.IsPositive && account.Balance >= account.ReservedForPayroll)
                {
                    account.InsolventCycles = 0;
                    Transition(ref account, CompanySolvency.Tight, tick);
                }
                else if (account.InsolventCycles >= economy.Options.LiquidationGraceCycles)
                {
                    Liquidate(tick, company, account);
                    return;
                }
                break;
        }

        economy.Accounts.Set(company, account);
    }

    private bool HasDefaultedLoan(Entity company)
    {
        foreach (var loan in economy.Loans.ForCompany(company))
        {
            if (loan.State == LoanState.Defaulted)
                return true;
        }
        return false;
    }

    private void Restructure(long tick, Entity company)
    {
        var actions = new List<string>();

        if (HasEmployees(company))
        {
            var employees = new List<(Entity Entity, Employee Employee)>();
            var query = new QueryDescription().WithAll<Employee>();
            world.Query(in query, (Entity entity, ref Employee employee) =>
            {
                if (employee.Company == company)
                    employees.Add((entity, employee));
            });

            foreach (var (entity, employee) in employees)
            {
                if (!world.IsAlive(entity))
                    continue;
                ref var live = ref world.Get<Employee>(entity);
                live.SalaryPerCycle = live.SalaryPerCycle.Multiply(0.90m).Max(Money.Zero);
            }

            if (employees.Count > 0)
                actions.Add("salary-cut-10%");
        }

        var closed = CloseOneEmptyOffice(company);
        if (closed)
            actions.Add("office-closed");

        if (actions.Count > 0)
        {
            economy.Emit(new CompanyRestructured(
                CompanyAccountService.NetworkIdOf(world, company),
                string.Join(",", actions),
                tick));
        }
    }

    private bool HasEmployees(Entity company)
    {
        return economy.Employment.CountEmployees(company) > 0;
    }

    private bool CloseOneEmptyOffice(Entity company)
    {
        var employeeCountByOffice = new Dictionary<Entity, int>();
        var employeeQuery = new QueryDescription().WithAll<Employee>();
        world.Query(in employeeQuery, (ref Employee employee) =>
        {
            if (employee.Company != company)
                return;
            employeeCountByOffice.TryGetValue(employee.Office, out var count);
            employeeCountByOffice[employee.Office] = count + 1;
        });

        var reservedByOffice = new Dictionary<Entity, int>();
        var postingQuery = new QueryDescription().WithAll<JobPosting>();
        world.Query(in postingQuery, (ref JobPosting posting) =>
        {
            if (posting.Company != company)
                return;
            reservedByOffice.TryGetValue(posting.Office, out var count);
            reservedByOffice[posting.Office] = count + posting.OpenSeats;
        });

        Entity candidate = Entity.Null;
        var officeQuery = new QueryDescription().WithAll<Office>();
        world.Query(in officeQuery, (Entity entity, ref Office office) =>
        {
            if (candidate != Entity.Null || office.Company != company || office.IsClosed)
                return;
            if (employeeCountByOffice.GetValueOrDefault(entity) > 0)
                return;
            if (reservedByOffice.GetValueOrDefault(entity) > 0)
                return;
            candidate = entity;
        });

        if (candidate == Entity.Null)
            return false;

        ref var target = ref world.Get<Office>(candidate);
        target.IsClosed = true;
        target.SeatsUsed = 0;
        return true;
    }

    private void Liquidate(long tick, Entity company, CompanyAccount account)
    {
        // 1. Stop all postings and reject pending applications.
        var postings = new List<Entity>();
        var postingQuery = new QueryDescription().WithAll<JobPosting>();
        world.Query(in postingQuery, (Entity entity, ref JobPosting posting) =>
        {
            if (posting.Company == company)
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

        var applications = new List<Entity>();
        var applicationQuery = new QueryDescription().WithAll<JobApplication>();
        world.Query(in applicationQuery, (Entity entity, ref JobApplication application) =>
        {
            if (application.Company == company && application.Status == ApplicationStatus.Pending)
                applications.Add(entity);
        });
        foreach (var application in applications)
        {
            if (world.IsAlive(application))
                world.Get<JobApplication>(application).Status = ApplicationStatus.Rejected;
        }

        // 2. Separate employees and create severance/arrears claims.
        var employees = new List<(Entity Entity, Employee Employee)>();
        var employeeQuery = new QueryDescription().WithAll<Employee>();
        world.Query(in employeeQuery, (Entity entity, ref Employee employee) =>
        {
            if (employee.Company == company)
                employees.Add((entity, employee));
        });

        foreach (var (entity, employee) in employees)
        {
            if (!world.IsAlive(entity))
                continue;

            var severance = employee.SalaryPerCycle * economy.Options.SeveranceCycles;
            world.Remove<Employee>(entity);
            economy.Employment.Remove(entity);

            if (world.Has<WageClaim>(entity))
            {
                ref var claim = ref world.Get<WageClaim>(entity);
                claim.Severance += severance;
                claim.Company = company;
            }
            else
            {
                world.Add(entity, new WageClaim(company, Money.Zero, Money.Zero, tick)
                {
                    Severance = severance
                });
            }

            economy.Emit(new EmployeeFired(
                CompanyAccountService.NetworkIdOf(world, company),
                CompanyAccountService.NetworkIdOf(world, entity)));
        }

        // 3. Pay claims in priority order: wages/severance, tax arrears, then public account.
        var claims = new List<(Entity Entity, WageClaim Claim)>();
        var claimQuery = new QueryDescription().WithAll<WageClaim>();
        world.Query(in claimQuery, (Entity entity, ref WageClaim claim) =>
        {
            if (claim.Company == company)
                claims.Add((entity, claim));
        });
        claims.Sort((a, b) => a.Claim.FirstMissedTick.CompareTo(b.Claim.FirstMissedTick));

        var paid = Money.Zero;
        var writtenOff = Money.Zero;
        var toRemove = new List<Entity>();

        account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
        foreach (var (entity, claim) in claims)
        {
            if (!world.IsAlive(entity))
                continue;

            var owed = claim.UnpaidNet + claim.Severance;
            if (!owed.IsPositive)
            {
                toRemove.Add(entity);
                continue;
            }

            var payable = account.Balance.Min(owed);
            if (payable.IsPositive)
            {
                if (!world.Has<Wallet>(entity))
                    world.Add(entity, new Wallet(Money.Zero));

                ref var wallet = ref world.Get<Wallet>(entity);
                wallet.Balance += payable;
                account.Balance -= payable;
                paid += payable;

                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.SeverancePaid,
                    CompanyAccountService.NetworkIdOf(world, company),
                    CompanyAccountService.NetworkIdOf(world, entity),
                    payable,
                    "liquidation.claim");
            }

            writtenOff += owed - payable;
            if (payable >= owed)
            {
                toRemove.Add(entity);
            }
            else
            {
                ref var liveClaim = ref world.Get<WageClaim>(entity);
                var remaining = payable;
                var severancePaid = remaining.Min(liveClaim.Severance);
                liveClaim.Severance -= severancePaid;
                remaining -= severancePaid;
                var netPaid = remaining.Min(liveClaim.UnpaidNet);
                liveClaim.UnpaidNet -= netPaid;
                if (liveClaim.UnpaidNet.IsZero)
                    liveClaim.UnpaidGross = Money.Zero;
                else
                    liveClaim.UnpaidGross = (liveClaim.UnpaidGross - netPaid).Max(Money.Zero);
            }
        }

        foreach (var entity in toRemove)
        {
            if (world.IsAlive(entity) && world.Has<WageClaim>(entity))
                world.Remove<WageClaim>(entity);
        }

        var taxDue = account.TaxArrears.Min(account.Balance);
        if (taxDue.IsPositive)
        {
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.CorporateTaxPaid,
                CompanyAccountService.NetworkIdOf(world, company),
                EconomyAccountIds.Government,
                taxDue,
                "liquidation.tax");
            economy.Government.Balance += taxDue;
            account.Balance -= taxDue;
            account.TaxArrears -= taxDue;
        }

        if (account.Balance.IsPositive)
        {
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.WriteOff,
                CompanyAccountService.NetworkIdOf(world, company),
                EconomyAccountIds.Government,
                account.Balance,
                "liquidation.residual");
            economy.Government.Balance += account.Balance;
            account.Balance = Money.Zero;
        }

        // 4. Close offices.
        var officeQuery = new QueryDescription().WithAll<Office>();
        world.Query(in officeQuery, (ref Office office) =>
        {
            if (office.Company == company)
            {
                office.IsClosed = true;
                office.SeatsUsed = 0;
            }
        });

        account.Arrears = Money.Zero;
        account.State = CompanySolvency.Bankrupt;
        account.StateChangedAtTick = tick;
        economy.Accounts.Set(company, account);

        economy.Emit(new CompanyLiquidated(
            CompanyAccountService.NetworkIdOf(world, company),
            paid.Cents,
            writtenOff.Cents,
            account.TaxArrears.Cents,
            tick));
    }

    private static void Transition(ref CompanyAccount account, CompanySolvency state, long tick)
    {
        if (account.State == state)
            return;
        account.State = state;
        account.StateChangedAtTick = tick;
    }
}
