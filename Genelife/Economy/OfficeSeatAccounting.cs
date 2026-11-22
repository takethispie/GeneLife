using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;

namespace Genelife.Economy;

/// <summary>
/// Pure seat accounting helpers. Used seats are employees assigned to an office
/// plus open seats reserved by postings tied to that office.
/// </summary>
public static class OfficeSeatAccounting
{
    public static int EmployeesInOffice(World world, Entity office)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<Employee>();
        world.Query(in query, (ref Employee employee) =>
        {
            if (employee.Office == office)
                count++;
        });
        return count;
    }

    public static int OpenSeatsForOffice(World world, Entity office)
    {
        var open = 0;
        var postingQuery = new QueryDescription().WithAll<JobPosting>();
        world.Query(in postingQuery, (Entity postingEntity, ref JobPosting posting) =>
        {
            if (posting.Office != office || posting.IsClosed)
                return;
            open += OpenSeatsForPosting(world, postingEntity);
        });
        return open;
    }

    public static int OpenSeatsForPosting(World world, Entity posting)
    {
        var open = 0;
        var query = new QueryDescription().WithAll<OpenPosition>();
        world.Query(in query, (ref OpenPosition seat) =>
        {
            if (seat.Posting == posting)
                open++;
        });
        return open;
    }

    public static int UsedSeats(World world, Entity office) =>
        EmployeesInOffice(world, office) + OpenSeatsForOffice(world, office);

    public static int AvailableSeats(World world, Entity office)
    {
        if (!world.IsAlive(office) || !world.Has<Office>(office))
            return 0;
        var capacity = world.Get<Office>(office).Capacity;
        return Math.Max(0, capacity - UsedSeats(world, office));
    }

    public static void RefreshUsedSeats(World world, Entity office)
    {
        if (!world.IsAlive(office) || !world.Has<Office>(office))
            return;
        ref var component = ref world.Get<Office>(office);
        component.SeatsUsed = UsedSeats(world, office);
    }
}
