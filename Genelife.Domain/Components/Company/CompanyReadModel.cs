using Arch.Core;

namespace Genelife.Components.Company;

public readonly record struct CompanyFinancialSnapshot(
    decimal Balance,
    decimal ReservedForPayroll,
    decimal Arrears,
    decimal RevenueLastCycle,
    decimal WagesLastCycle,
    string Solvency)
{
    public static CompanyFinancialSnapshot From(CompanyAccount account) => new(
        account.Balance.Units,
        account.ReservedForPayroll.Units,
        account.Arrears.Units,
        account.RevenueLastCycle.Units,
        account.WagesLastCycle.Units,
        account.State.ToString());
}

public static class CompanyOfficeCounter
{
    public static Dictionary<Entity, int> CountOpenByCompany(World world)
    {
        var counts = new Dictionary<Entity, int>();
        var query = new QueryDescription().WithAll<Office>();
        world.Query(in query, (ref Office office) =>
        {
            if (office.IsClosed)
                return;

            counts.TryGetValue(office.Company, out var count);
            counts[office.Company] = count + 1;
        });

        return counts;
    }
}
