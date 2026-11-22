using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Life;

/// <summary>
/// Career ladder and salary bands. Bands are multipliers of a role's base
/// salary; <see cref="BaseSalary"/> is the default for a newly created posting.
/// </summary>
public sealed class CareerPolicy
{
    public Money BaseSalary { get; set; } = Money.FromUnits(1_000m);
    public Money SalaryPerSkillPoint { get; set; } = Money.FromUnits(150m);
    public Money MinimumSalary { get; set; } = Money.FromUnits(500m);
    public Money MaximumSalary { get; set; } = Money.FromUnits(8_000m);

    public float PromotionThreshold { get; set; } = 0.72f;
    public float DemotionThreshold { get; set; } = 0.30f;
    public decimal AnnualIndexation { get; set; } = 0.02m;
    public decimal PromotionRaise { get; set; } = 0.15m;
    public decimal DemotionCut { get; set; } = 0.10m;

    public static CareerPolicy Default { get; } = new();

    public Seniority Next(Seniority seniority) => seniority switch
    {
        Seniority.Associate => Seniority.Specialist,
        Seniority.Specialist => Seniority.Senior,
        Seniority.Senior => Seniority.Lead,
        Seniority.Lead => Seniority.Manager,
        Seniority.Manager => Seniority.Director,
        _ => Seniority.Director
    };

    public Seniority Previous(Seniority seniority) => seniority switch
    {
        Seniority.Director => Seniority.Manager,
        Seniority.Manager => Seniority.Lead,
        Seniority.Lead => Seniority.Senior,
        Seniority.Senior => Seniority.Specialist,
        Seniority.Specialist => Seniority.Associate,
        _ => Seniority.Associate
    };

    public Money SalaryForSkills(float skillTotal)
    {
        var salary = BaseSalary + SalaryPerSkillPoint.Multiply((decimal)Math.Clamp(skillTotal, 0f, 6f));
        return salary.Max(MinimumSalary).Min(MaximumSalary);
    }
}
