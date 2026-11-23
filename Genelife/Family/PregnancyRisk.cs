namespace Genelife.Family;

public static class PregnancyRisk
{
    public static int Compute(
        int age,
        int health,
        int missedCareMonths,
        int complications,
        ReproductionOptions? options = null)
    {
        options ??= ReproductionOptions.Default;

        var risk = 20;
        var riskyAge = options.MaximumConceptionAge - 10;
        if (age > riskyAge)
            risk += (age - riskyAge) * 15;
        if (health < 70)
            risk += (70 - Math.Clamp(health, 0, 100)) * 4;

        risk += Math.Max(0, missedCareMonths) * 80;
        risk += Math.Max(0, complications) * 100;
        return Math.Clamp(risk, 0, 1000);
    }

    public static decimal LossChance(int riskBasisPoints, ReproductionOptions? options = null)
    {
        options ??= ReproductionOptions.Default;
        return Math.Clamp(
            options.MonthlyLossChance + riskBasisPoints / 10_000m,
            0m,
            1m);
    }
}
