using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Owns job posting lifecycle and the seat invariants. One OpenPosition entity is
/// created per seat; a posting disappears when its last seat is filled.
/// </summary>
public sealed class JobPostingRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;

    public JobPostingRepository(World world, NetworkIdRegistry networkIds, EconomyContext? economy = null)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
    }

    public NetworkId Open(
        NetworkId companyId,
        NetworkId officeId,
        string position,
        Money salary,
        Skill requirements,
        int seats,
        long tick = 0)
    {
        if (string.IsNullOrWhiteSpace(position))
            throw new ArgumentException("Position is required.", nameof(position));
        if (seats <= 0)
            throw new ArgumentOutOfRangeException(nameof(seats), "Seats must be positive.");
        if (!salary.IsPositive)
            throw new ArgumentOutOfRangeException(nameof(salary), "Salary must be positive.");

        if (!networkIds.TryGetEntity(companyId, out var company) ||
            !world.IsAlive(company) ||
            !world.Has<CompanyName>(company))
            throw new InvalidOperationException("Company not found.");

        if (!networkIds.TryGetEntity(officeId, out var officeEntity) ||
            !world.IsAlive(officeEntity) ||
            !world.Has<Office>(officeEntity))
            throw new InvalidOperationException("Office not found.");

        ref var office = ref world.Get<Office>(officeEntity);
        if (office.Company != company)
            throw new InvalidOperationException("The office does not belong to the company.");
        if (office.IsClosed)
            throw new InvalidOperationException("The office is closed.");

        if (OfficeSeatAccounting.AvailableSeats(world, officeEntity) < seats)
            throw new InvalidOperationException("Not enough office capacity for the requested seats.");

        if (economy is not null &&
            economy.Accounts.TryGet(company, out var account) &&
            (account.State is CompanySolvency.Insolvent or CompanySolvency.Bankrupt ||
             account.Arrears.IsPositive))
            throw new InvalidOperationException("A company in arrears or insolvency cannot open postings.");

        var postingEntity = world.Create(new JobPosting(
            company,
            officeEntity,
            position,
            salary,
            requirements,
            seats,
            seats,
            tick));
        var postingId = networkIds.Allocate(postingEntity);
        world.Add(postingEntity, postingId);

        for (var i = 0; i < seats; i++)
            world.Create(new OpenPosition(postingEntity));

        office.SeatsUsed = OfficeSeatAccounting.UsedSeats(world, officeEntity);

        economy?.Emit(new JobPostingOpened(
            postingId.Value,
            companyId.Value,
            officeId.Value,
            position,
            salary.Cents,
            seats));
        economy?.Emit(new JobSeatReserved(postingId.Value, seats));

        return postingId;
    }

    public bool UpdateSalary(NetworkId postingId, Money salary, long tick = 0)
    {
        if (!TryGet(postingId, out var entity))
            return false;
        if (!salary.IsPositive)
            throw new ArgumentOutOfRangeException(nameof(salary), "Salary must be positive.");

        ref var posting = ref world.Get<JobPosting>(entity);
        if (posting.IsClosed)
            throw new InvalidOperationException("The posting is closed.");
        posting.SalaryPerCycle = salary;
        return true;
    }

    public bool Resize(NetworkId postingId, int seats, long tick = 0)
    {
        if (!TryGet(postingId, out var entity))
            return false;
        if (seats <= 0)
            throw new ArgumentOutOfRangeException(nameof(seats), "Seats must be positive.");

        ref var posting = ref world.Get<JobPosting>(entity);
        if (posting.IsClosed)
            throw new InvalidOperationException("The posting is closed.");

        var openSeats = OfficeSeatAccounting.OpenSeatsForPosting(world, entity);
        var filled = Math.Max(0, posting.TotalSeats - openSeats);
        if (seats < filled)
            throw new InvalidOperationException("Seat count cannot be below the number of filled seats.");

        if (seats > openSeats)
        {
            var additional = seats - openSeats;
            if (OfficeSeatAccounting.AvailableSeats(world, posting.Office) < additional)
                throw new InvalidOperationException("Not enough office capacity for the additional seats.");
            for (var i = 0; i < additional; i++)
                world.Create(new OpenPosition(entity));
        }
        else if (seats < openSeats)
        {
            var toRemove = openSeats - seats;
            var seatEntities = new List<Entity>();
            var query = new QueryDescription().WithAll<OpenPosition>();
            world.Query(in query, (Entity seatEntity, ref OpenPosition seat) =>
            {
                if (seat.Posting == entity && seatEntities.Count < toRemove)
                    seatEntities.Add(seatEntity);
            });
            foreach (var seatEntity in seatEntities)
            {
                if (world.IsAlive(seatEntity))
                    world.Destroy(seatEntity);
            }
        }

        posting.TotalSeats = seats;
        posting.OpenSeats = seats - filled;
        OfficeSeatAccounting.RefreshUsedSeats(world, posting.Office);

        if (posting.OpenSeats > openSeats)
            economy?.Emit(new JobSeatReserved(postingId.Value, posting.OpenSeats - openSeats));
        else if (posting.OpenSeats < openSeats)
            economy?.Emit(new JobSeatReleased(postingId.Value, openSeats - posting.OpenSeats));

        return true;
    }

    public bool Close(NetworkId postingId, long tick = 0)
    {
        if (!TryGet(postingId, out var entity))
            return false;

        var posting = world.Get<JobPosting>(entity);
        var seats = new List<Entity>();
        var seatQuery = new QueryDescription().WithAll<OpenPosition>();
        world.Query(in seatQuery, (Entity seatEntity, ref OpenPosition seat) =>
        {
            if (seat.Posting == entity)
                seats.Add(seatEntity);
        });

        var applications = new List<Entity>();
        var appQuery = new QueryDescription().WithAll<JobApplication>();
        world.Query(in appQuery, (Entity appEntity, ref JobApplication app) =>
        {
            if (app.JobPosting == entity && app.Status == ApplicationStatus.Pending)
                applications.Add(appEntity);
        });

        foreach (var appEntity in applications)
        {
            if (world.IsAlive(appEntity))
                world.Get<JobApplication>(appEntity).Status = ApplicationStatus.Rejected;
        }

        foreach (var seatEntity in seats)
        {
            if (world.IsAlive(seatEntity))
                world.Destroy(seatEntity);
        }

        networkIds.Remove(postingId);
        world.Destroy(entity);

        if (world.IsAlive(posting.Office))
        {
            ref var office = ref world.Get<Office>(posting.Office);
            office.SeatsUsed = OfficeSeatAccounting.UsedSeats(world, posting.Office);
        }

        economy?.Emit(new JobPostingClosed(
            postingId.Value,
            world.Has<NetworkId>(posting.Company) ? world.Get<NetworkId>(posting.Company).Value : 0UL));
        return true;
    }

    public int CloseAllForCompany(NetworkId companyId, long tick = 0)
    {
        if (!networkIds.TryGetEntity(companyId, out var company))
            return 0;

        var postings = new List<NetworkId>();
        var query = new QueryDescription().WithAll<JobPosting, NetworkId>();
        world.Query(in query, (ref JobPosting posting, ref NetworkId networkId) =>
        {
            if (posting.Company == company)
                postings.Add(networkId);
        });

        foreach (var postingId in postings)
            Close(postingId, tick);

        return postings.Count;
    }

    public bool TryGet(NetworkId postingId, out Entity entity)
    {
        if (networkIds.TryGetEntity(postingId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<JobPosting>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }
}
