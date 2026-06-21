namespace Genelife.Components;

/// <summary>
/// Component representing a Sim looking for work
/// </summary>
public record struct JobSeeker
{
    public bool IsLookingForWork;
    public List<string> Skills;
    public float DesiredSalary;
    
    public JobSeeker(bool isLookingForWork = true, List<string>? skills = null, float desiredSalary = 1000f)
    {
        IsLookingForWork = isLookingForWork;
        Skills = skills ?? [];
        DesiredSalary = desiredSalary;
    }
}
