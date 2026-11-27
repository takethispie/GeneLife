using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// Company twin of <see cref="GenosStatusEqualityTests"/>. CompaniesEqual compared three of ten
/// fields, so office count, balance, reserved payroll, arrears, revenue, wages and solvency never
/// produced a delta and clients held stale company financials.
/// </summary>
public class CompanyStatusEqualityTests
{
    [Fact]
    public void IdenticalCopies_CompareEqual()
    {
        var baseline = Baseline();

        Assert.True(WorldSnapshotDelta.CompaniesEqual(baseline, DtoGuard.Clone(baseline)));
    }

    [Fact]
    public void EveryProperty_ParticipatesInEquality()
    {
        var ignored = DtoGuard.IgnoredProperties(Baseline(), WorldSnapshotDelta.CompaniesEqual);

        Assert.True(
            ignored.Count == 0,
            $"CompaniesEqual ignores these fields, so changes to them produce no delta: {string.Join(", ", ignored)}");
    }

    private static CompanyStatus Baseline() => new()
    {
        NetworkId = 7,
        Name = "Acme",
        EmployeeCount = 12,
        OfficeCount = 3,
        Balance = 1234.56m,
        ReservedForPayroll = 789.01m,
        Arrears = 23.45m,
        RevenueLastCycle = 4321.09m,
        WagesLastCycle = 876.54m,
        Solvency = "Stable"
    };
}