using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class UpdateEmployeeProductivity {
    public Employee Execute(Employee employment, Random random)
    {
        // Simulate productivity changes based on various factors
        var productivityChange = random.NextSingle() * 0.4 - 0.2; // -0.2 to +0.2
        var newProductivity = Math.Max(0.1f, Math.Min(2.0f, employment.ProductivityScore + productivityChange));
        return employment with { ProductivityScore = Convert.ToSingle(newProductivity) };
    }
}