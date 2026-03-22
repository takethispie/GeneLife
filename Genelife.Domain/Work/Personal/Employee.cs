namespace Genelife.Domain.Work.Personal;

public class Employee(
    Guid id,
    float salary,
    DateTime hireDate,
    EmploymentStatus status,
    float productivityScore = 1.0f
)
{
    public Guid Id { get; init; } = id;
    public float Salary { get; private set; } = salary;
    public DateTime HireDate { get; private set; } = hireDate;
    public EmploymentStatus Status { get; private set; } = status;
    public float ProductivityScore { get; private set; } = productivityScore;

    public float CalculateProductivity()
    {
        var productivityChange = new Random().NextSingle() * 0.4 - 0.2; 
        var newProductivity = Math.Max(0.1f, Math.Min(2.0f, ProductivityScore + productivityChange));
        ProductivityScore = Convert.ToSingle(newProductivity);
        return ProductivityScore;
    }
}