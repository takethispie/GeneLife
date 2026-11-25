using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Company-funded payroll. Wages are paid gross per pay cycle; employee pension
/// and income tax are withheld and employer pension is added on top. The company
/// account is debited before any wallet is credited, and shortfalls become
/// WageClaim arrears instead of creating money.
/// </summary>
public class PayrollSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription employeeQuery;
    private readonly QueryDescription claimQuery;
    private readonly int payrollInterval;
    private readonly int cadenceOffset;

    public PayrollSystem(World world, EconomyContext economy, int? payrollInterval = null, int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.payrollInterval = payrollInterval ?? economy.Clock.TicksPerPayCycle;
        this.cadenceOffset = cadenceOffset;
        employeeQuery = new QueryDescription().WithAll<Employee>().WithNone<Components.Gen.Dead>();
        claimQuery = new QueryDescription().WithAll<WageClaim>();
    }

    public string Name => "Payroll";
    public int Order => 500;
    public Cadence Cadence => new(payrollInterval <= 0 ? 1 : payrollInterval, cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context);

    public void Update(in SimulationContext context)
    {
        var employees = new List<EmployeeRecord>();
        world.Query(in employeeQuery, (Entity entity, ref Employee employee) =>
        {
            employees.Add(new EmployeeRecord(entity, employee));
        });

        var byCompany = new Dictionary<Entity, List<EmployeeRecord>>();
        foreach (var record in employees)
        {
            if (!byCompany.TryGetValue(record.Employee.Company, out var list))
            {
                list = new List<EmployeeRecord>();
                byCompany[record.Employee.Company] = list;
            }
            list.Add(record);
        }

        foreach (var (company, records) in byCompany)
        {
            PayCompany(context.Tick, company, records);
        }

        PayArrears(context.Tick);
    }

    private void PayCompany(long tick, Entity company, List<EmployeeRecord> records)
    {
        var openingAccount = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
        var policy = economy.Options;

        var grossTotal = Money.Zero;
        var employerPensionTotal = Money.Zero;
        var cashNeeded = Money.Zero;
        var plan = new List<PayLine>(records.Count);

        foreach (var record in records)
        {
            var employee = record.Employee;
            var gross = employee.SalaryPerCycle;
            var employeePension = gross.Multiply(policy.EmployeePensionRate);
            var incomeTax = gross.Multiply(policy.IncomeTaxRate);
            var employerPension = gross.Multiply(policy.EmployerPensionRate);
            var net = gross - employeePension - incomeTax;
            if (net.IsNegative)
                net = Money.Zero;

            grossTotal += gross;
            employerPensionTotal += employerPension;
            cashNeeded += net + employeePension + incomeTax + employerPension;
            plan.Add(new PayLine(record.Entity, employee, gross, net, employeePension, incomeTax, employerPension));
        }

        var scale = openingAccount.Balance >= cashNeeded || cashNeeded.IsZero
            ? 1m
            : openingAccount.Balance.Cents / (decimal)cashNeeded.Cents;

        var unpaidNetTotal = Money.Zero;
        var deferredAny = false;

        foreach (var line in plan)
        {
            var paidNet = ScaleDown(line.Net, scale);
            var paidEmployeePension = ScaleDown(line.EmployeePension, scale);
            var paidIncomeTax = ScaleDown(line.IncomeTax, scale);
            var paidEmployerPension = ScaleDown(line.EmployerPension, scale);

            if (paidNet.IsPositive)
            {
                if (!world.Has<Wallet>(line.Entity))
                    world.Add(line.Entity, new Wallet(Money.Zero));

                ref var wallet = ref world.Get<Wallet>(line.Entity);
                wallet.Balance += paidNet;
                economy.Accounts.TryDebit(
                    company,
                    paidNet,
                    LedgerKind.WagesPaid,
                    Guid.NewGuid(),
                    CompanyAccountService.NetworkIdOf(world, line.Entity),
                    $"payroll.wage.{line.Entity.Id}",
                    tick);
                economy.WagesPaidThisYear += paidNet;
            }

            if (paidIncomeTax.IsPositive)
            {
                if (economy.Accounts.TryDebit(
                        company,
                        paidIncomeTax,
                        LedgerKind.IncomeTaxPaid,
                        Guid.NewGuid(),
                        EconomyAccountIds.Government,
                        "payroll.income-tax",
                        tick))
                {
                    economy.Government.Balance += paidIncomeTax;
                    economy.Government.IncomeTaxCollected += paidIncomeTax;
                    economy.TaxThisYear += paidIncomeTax;
                }
            }

            var pensionPaid = paidEmployeePension + paidEmployerPension;
            if (pensionPaid.IsPositive)
            {
                if (economy.Accounts.TryDebit(
                        company,
                        pensionPaid,
                        LedgerKind.PensionContribution,
                        Guid.NewGuid(),
                        EconomyAccountIds.PensionFund,
                        "payroll.pension",
                        tick))
                {
                    economy.Pension.Balance += pensionPaid;
                    economy.Pension.ContributionsThisCycle += pensionPaid;
                    CreditPensionStatement(line, paidEmployeePension, paidEmployerPension, tick);
                }
            }

            var paidGross = paidNet + paidEmployeePension + paidIncomeTax;
            var unpaidNet = line.Net - paidNet;
            var unpaidGross = line.Gross - paidGross;

            if (unpaidNet.IsPositive || unpaidGross.IsPositive)
            {
                deferredAny = true;
                unpaidNetTotal += unpaidNet;
                RecordClaim(line, company, unpaidGross, unpaidNet, tick);
                economy.Emit(new WagesDeferred(
                    CompanyAccountService.NetworkIdOf(world, company),
                    CompanyAccountService.NetworkIdOf(world, line.Entity),
                    unpaidGross.Cents,
                    unpaidNet.Cents,
                    tick));
            }

            economy.Emit(new WagesPaid(
                CompanyAccountService.NetworkIdOf(world, company),
                CompanyAccountService.NetworkIdOf(world, line.Entity),
                line.Gross.Cents,
                paidNet.Cents,
                paidIncomeTax.Cents,
                (paidEmployeePension + paidEmployerPension).Cents,
                tick));
        }

        var account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
        account.ReservedForPayroll = cashNeeded;
        account.WagesLastCycle += grossTotal;
        account.CostsLastCycle += employerPensionTotal;

        if (deferredAny)
        {
            account.Arrears += unpaidNetTotal;
            account.ArrearsCycles++;
            SetState(ref account, CompanySolvency.Arrears, tick);
            economy.Emit(new PayrollShortfall(
                CompanyAccountService.NetworkIdOf(world, company),
                unpaidNetTotal.Cents,
                account.Balance.Cents,
                tick));
        }
        else
        {
            account.ArrearsCycles = 0;
            SetState(ref account, account.Balance >= account.ReservedForPayroll
                ? CompanySolvency.Healthy
                : CompanySolvency.Tight, tick);
        }

        economy.Accounts.Set(company, account);
        economy.Emit(new PayrollCycleCompleted(
            CompanyAccountService.NetworkIdOf(world, company),
            grossTotal.Cents,
            cashNeeded.Cents,
            account.Balance.Cents,
            tick));
    }

    private void CreditPensionStatement(PayLine line, Money employee, Money employer, long tick)
    {
        if (!world.Has<PensionAccount>(line.Entity))
            world.Add(line.Entity, new PensionAccount());

        ref var account = ref world.Get<PensionAccount>(line.Entity);
        account.EmployeeContributions += employee;
        account.EmployerContributions += employer;
        account.ContributingCycles++;
        account.LastContributionTick = tick;
        economy.Emit(new PensionAccrued(
            CompanyAccountService.NetworkIdOf(world, line.Entity),
            employee.Cents,
            employer.Cents,
            account.ContributingCycles,
            tick));
    }

    private void RecordClaim(PayLine line, Entity company, Money unpaidGross, Money unpaidNet, long tick)
    {
        if (world.Has<WageClaim>(line.Entity))
        {
            ref var existing = ref world.Get<WageClaim>(line.Entity);
            existing.UnpaidGross += unpaidGross;
            existing.UnpaidNet += unpaidNet;
            existing.CyclesInArrears++;
            if (existing.Company == Entity.Null)
                existing.Company = company;
            return;
        }

        world.Add(line.Entity, new WageClaim(company, unpaidGross, unpaidNet, tick));
    }

    private void PayArrears(long tick)
    {
        var claims = new List<(Entity Entity, WageClaim Claim)>();
        world.Query(in claimQuery, (Entity entity, ref WageClaim claim) =>
        {
            claims.Add((entity, claim));
        });

        claims.Sort((a, b) => a.Claim.FirstMissedTick.CompareTo(b.Claim.FirstMissedTick));

        var toRemove = new List<Entity>();
        foreach (var (entity, claim) in claims)
        {
            if (!world.IsAlive(entity) || !world.IsAlive(claim.Company))
                continue;

            if (!economy.Accounts.TryGet(claim.Company, out var companyAccount))
                continue;

            var owed = claim.UnpaidNet + claim.Severance;
            if (!owed.IsPositive)
            {
                toRemove.Add(entity);
                continue;
            }

            var payable = companyAccount.Balance.Min(owed);
            if (!payable.IsPositive)
                continue;

            if (!world.Has<Wallet>(entity))
                world.Add(entity, new Wallet(Money.Zero));

            ref var wallet = ref world.Get<Wallet>(entity);
            wallet.Balance += payable;

            economy.Accounts.TryDebit(
                claim.Company,
                payable,
                LedgerKind.WageArrearsSettled,
                Guid.NewGuid(),
                CompanyAccountService.NetworkIdOf(world, entity),
                "payroll.arrears",
                tick);

            companyAccount = world.Get<CompanyAccount>(claim.Company);
            companyAccount.Arrears = (companyAccount.Arrears - payable).Max(Money.Zero);
            economy.Accounts.Set(claim.Company, companyAccount);

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

            if (liveClaim.UnpaidNet.IsZero && liveClaim.UnpaidGross.IsZero && liveClaim.Severance.IsZero)
            {
                toRemove.Add(entity);
                if (companyAccount.Arrears.IsZero)
                {
                    companyAccount.ArrearsCycles = 0;
                    SetState(ref companyAccount, companyAccount.Balance >= companyAccount.ReservedForPayroll
                        ? CompanySolvency.Healthy
                        : CompanySolvency.Tight, tick);
                    economy.Accounts.Set(claim.Company, companyAccount);
                }
            }

            economy.Emit(new WageArrearsSettled(
                CompanyAccountService.NetworkIdOf(world, claim.Company),
                CompanyAccountService.NetworkIdOf(world, entity),
                payable.Cents,
                tick));
        }

        foreach (var entity in toRemove)
        {
            if (world.IsAlive(entity) && world.Has<WageClaim>(entity))
                world.Remove<WageClaim>(entity);
        }
    }

    private static Money ScaleDown(Money amount, decimal scale)
    {
        if (scale >= 1m)
            return amount;
        return new Money((long)Math.Floor(amount.Cents * scale));
    }

    private static void SetState(ref CompanyAccount account, CompanySolvency state, long tick)
    {
        if (account.State == state)
            return;

        account.State = state;
        account.StateChangedAtTick = tick;
    }

    private readonly record struct EmployeeRecord(Entity Entity, Employee Employee);

    private readonly record struct PayLine(
        Entity Entity,
        Employee Employee,
        Money Gross,
        Money Net,
        Money EmployeePension,
        Money IncomeTax,
        Money EmployerPension);
}
