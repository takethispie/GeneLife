namespace Genelife.Domain;

public record HumanSkills(
    Guid HumanId,
    List<string> TechnicalSkills,
    List<string> SoftSkills,
    int YearsOfExperience,
    JobLevel CurrentLevel,
    decimal DesiredSalary
);