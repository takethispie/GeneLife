using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;
using Genelife.Systems;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Company-initiated and worker-initiated separations. Severance is paid when
/// cash exists, otherwise it becomes a WageClaim; the seat is recreated only
/// when an open posting for the same company/office/position still exists.
/// </summary>
public sealed class EmploymentRepository
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;

    public EmploymentRepository(
        World world,
        EmploymentIndex index,
        NetworkIdRegistry networkIds,
        EconomyContext? economy = null)
    {
        this.world = world;
        this.index = index;
        this.networkIds = networkIds;
        this.economy = economy;
    }

    public bool Fire(ulong simNetworkId, long tick, bool companyInitiated = true)
        => Separate(simNetworkId, tick, companyInitiated);

    public bool Resign(ulong simNetworkId, long tick)
        => Separate(simNetworkId, tick, companyInitiated: false);

    private bool Separate(ulong simNetworkId, long tick, bool companyInitiated)
    {
        if (!networkIds.TryGetEntity(new NetworkId(simNetworkId), out var sim) ||
            !world.IsAlive(sim) ||
            !world.Has<Employee>(sim))
            return false;

        var employee = world.Get<Employee>(sim);
        var company = employee.Company;
        var severance = companyInitiated
            ? employee.SalaryPerCycle * (economy?.Options.SeveranceCycles ?? 2)
            : Money.Zero;

        LifeSystemHelpers.RemoveEmployment(world, index, economy, sim, tick);

        if (!world.Has<Wallet>(sim))
            world.Add(sim, new Wallet(Money.Zero));

        var isWorkStage = !world.Has<Lifecycle>(sim) || world.Get<Lifecycle>(sim).Stage == LifeStage.Work;
        if (isWorkStage && !world.Has<JobSeeker>(sim))
        {
            var profile = world.Has<SkillProfile>(sim) ? world.Get<SkillProfile>(sim) : new SkillProfile();
            var desired = (employee.SalaryPerCycle * 0.9m)
                .Max(economy?.Life.Careers.MinimumSalary ?? Money.FromUnits(500m));
            world.Add(sim, new JobSeeker(true, profile.ToMask(), desired));
        }

        var paid = Money.Zero;
        if (severance.IsPositive && economy is not null)
        {
            if (economy.Accounts.TryGet(company, out var account) && account.Balance >= severance)
            {
                economy.Accounts.TryDebit(
                    company,
                    severance,
                    LedgerKind.SeverancePaid,
                    Guid.NewGuid(),
                    CompanyAccountService.NetworkIdOf(world, sim),
                    "severance",
                    tick);
                ref var wallet = ref world.Get<Wallet>(sim);
                wallet.Balance += severance;
                paid = severance;
            }
            else if (severance.IsPositive)
            {
                AddSeveranceClaim(sim, company, severance, tick);
            }
        }

        economy?.Emit(new EmployeeFired(
            CompanyAccountService.NetworkIdOf(world, company),
            simNetworkId));

        if (!companyInitiated)
            economy?.Emit(new EmployeeResigned(
                CompanyAccountService.NetworkIdOf(world, company),
                simNetworkId));

        return true;
    }

    private void AddSeveranceClaim(Entity sim, Entity company, Money severance, long tick)
    {
        if (world.Has<WageClaim>(sim))
        {
            ref var claim = ref world.Get<WageClaim>(sim);
            claim.Severance += severance;
            claim.Company = company;
            return;
        }

        world.Add(sim, new WageClaim(company, Money.Zero, Money.Zero, tick)
        {
            Severance = severance
        });
    }
}
