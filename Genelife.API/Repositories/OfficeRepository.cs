using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Creates, updates and closes offices. Closing is rejected while an office has
/// employees or open seats.
/// </summary>
public sealed class OfficeRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;

    public OfficeRepository(World world, NetworkIdRegistry networkIds, EconomyContext? economy = null)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
    }

    public NetworkId Add(
        NetworkId companyId,
        string name,
        int capacity,
        Money operatingCost,
        long tick = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Office name is required.", nameof(name));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Office capacity must be positive.");
        if (!networkIds.TryGetEntity(companyId, out var company) ||
            !world.IsAlive(company) ||
            !world.Has<CompanyName>(company))
            throw new InvalidOperationException("Company not found.");

        if (DuplicateName(company, name))
            throw new InvalidOperationException("The company already has an office with that name.");

        var entity = world.Create(new Office(
            name,
            company,
            capacity,
            operatingCost,
            seatsUsed: 0,
            isClosed: false));
        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);

        economy?.Emit(new Genelife.Messages.Events.OfficeCreated(
            networkId.Value,
            companyId.Value,
            name,
            capacity,
            operatingCost.Cents));

        return networkId;
    }

    public bool Update(
        NetworkId officeId,
        string? name,
        int? capacity,
        Money? operatingCost,
        long tick = 0)
    {
        if (!TryGet(officeId, out var entity))
            return false;

        ref var office = ref world.Get<Office>(entity);
        if (office.IsClosed)
            throw new InvalidOperationException("Cannot update a closed office.");
        if (capacity is <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Office capacity must be positive.");
        if (capacity is not null && capacity.Value < OfficeSeatAccounting.UsedSeats(world, entity))
            throw new InvalidOperationException("Capacity cannot be below the office's used seats.");
        if (!string.IsNullOrWhiteSpace(name) && DuplicateName(office.Company, name, entity))
            throw new InvalidOperationException("The company already has an office with that name.");

        if (!string.IsNullOrWhiteSpace(name))
            office.OfficeName = name;
        if (capacity is not null)
            office.Capacity = capacity.Value;
        if (operatingCost is not null)
            office.OperatingCostPerCycle = operatingCost.Value;

        office.SeatsUsed = OfficeSeatAccounting.UsedSeats(world, entity);

        economy?.Emit(new Genelife.Messages.Events.OfficeUpdated(
            officeId.Value,
            office.OfficeName,
            office.Capacity,
            office.OperatingCostPerCycle.Cents));
        return true;
    }

    public bool Close(NetworkId officeId)
    {
        if (!TryGet(officeId, out var entity))
            return false;

        if (OfficeSeatAccounting.UsedSeats(world, entity) > 0)
            throw new InvalidOperationException("Cannot close an office with employees or open seats.");

        ref var office = ref world.Get<Office>(entity);
        office.IsClosed = true;
        office.SeatsUsed = 0;

        economy?.Emit(new Genelife.Messages.Events.OfficeClosed(officeId.Value));
        return true;
    }

    public bool TryGet(NetworkId officeId, out Entity entity)
    {
        if (networkIds.TryGetEntity(officeId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<Office>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }

    public List<OfficeStatus> GetAll()
    {
        var offices = new List<OfficeStatus>();
        var query = new QueryDescription().WithAll<Office, NetworkId>();
        world.Query(in query, (Entity entity, ref Office office, ref NetworkId networkId) =>
        {
            var companyId = world.Has<NetworkId>(office.Company)
                ? world.Get<NetworkId>(office.Company).Value
                : 0UL;
            offices.Add(new OfficeStatus
            {
                NetworkId = networkId.Value,
                Name = office.OfficeName,
                CompanyNetworkId = companyId,
                Capacity = office.Capacity,
                EmployeeCount = OfficeSeatAccounting.EmployeesInOffice(world, entity),
                OpenSeats = OfficeSeatAccounting.OpenSeatsForOffice(world, entity),
                SeatsUsed = OfficeSeatAccounting.UsedSeats(world, entity),
                OperatingCost = office.OperatingCostPerCycle.Units,
                IsClosed = office.IsClosed
            });
        });
        return offices;
    }

    private bool DuplicateName(Entity company, string name, Entity? except = null)
    {
        var duplicate = false;
        var query = new QueryDescription().WithAll<Office>();
        world.Query(in query, (Entity entity, ref Office office) =>
        {
            if (duplicate || office.Company != company || entity == except)
                return;
            if (string.Equals(office.OfficeName, name, StringComparison.OrdinalIgnoreCase))
                duplicate = true;
        });
        return duplicate;
    }
}
