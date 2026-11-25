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

    public bool Fire(ulong genosNetworkId, long tick, bool companyInitiated = true)
        => Separate(genosNetworkId, tick, companyInitiated);

    public bool Resign(ulong genosNetworkId, long tick)
        => Separate(genosNetworkId, tick, companyInitiated: false);

    private bool Separate(ulong genosNetworkId, long tick, bool companyInitiated)
    {
        if (!networkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos) ||
            !world.IsAlive(genos) ||
            !world.Has<Employee>(genos))
            return false;

        var employee = world.Get<Employee>(genos);
        var company = employee.Company;
        var severance = companyInitiated
            ? employee.SalaryPerCycle * (economy?.Options.SeveranceCycles ?? 2)
            : Money.Zero;

        LifeSystemHelpers.RemoveEmployment(world, index, economy, genos, tick);

        if (!world.Has<Wallet>(genos))
            world.Add(genos, new Wallet(Money.Zero));

        var isWorkStage = !world.Has<Lifecycle>(genos) || world.Get<Lifecycle>(genos).Stage == LifeStage.Work;
        if (isWorkStage && !world.Has<JobSeeker>(genos))
        {
            var profile = world.Has<SkillProfile>(genos) ? world.Get<SkillProfile>(genos) : new SkillProfile();
            var desired = (employee.SalaryPerCycle * 0.9m)
                .Max(economy?.Life.Careers.MinimumSalary ?? Money.FromUnits(500m));
            world.Add(genos, new JobSeeker(true, profile.ToMask(), desired));
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
                    CompanyAccountService.NetworkIdOf(world, genos),
                    "severance",
                    tick);
                ref var wallet = ref world.Get<Wallet>(genos);
                wallet.Balance += severance;
                paid = severance;
            }
            else if (severance.IsPositive)
            {
                AddSeveranceClaim(genos, company, severance, tick);
            }
        }

        economy?.Emit(new EmployeeFired(
            CompanyAccountService.NetworkIdOf(world, company),
            genosNetworkId));

        if (!companyInitiated)
            economy?.Emit(new EmployeeResigned(
                CompanyAccountService.NetworkIdOf(world, company),
                genosNetworkId));

        return true;
    }

    private void AddSeveranceClaim(Entity genos, Entity company, Money severance, long tick)
    {
        if (world.Has<WageClaim>(genos))
        {
            ref var claim = ref world.Get<WageClaim>(genos);
            claim.Severance += severance;
            claim.Company = company;
            return;
        }

        world.Add(genos, new WageClaim(company, Money.Zero, Money.Zero, tick)
        {
            Severance = severance
        });
    }
}
