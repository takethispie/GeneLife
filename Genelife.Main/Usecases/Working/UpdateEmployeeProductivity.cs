using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class UpdateEmployeeProductivity {
    public Employee Execute(Employee employment)
    {
        // -0.2 to +0.2
        var productivityChange = new Random().NextSingle() * 0.4 - 0.2; 
        var newProductivity = Math.Max(0.1f, Math.Min(2.0f, employment.ProductivityScore + productivityChange));
        return employment with { ProductivityScore = Convert.ToSingle(newProductivity) };
    }
}