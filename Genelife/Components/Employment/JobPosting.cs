namespace Genelife.Components.Employment;

/// <summary>
/// Component representing a job posting from a company
/// </summary>
public record struct JobPosting
{
    public int CompanyEntityId;
    public int OfficeEntityId;
    public string Position;
    public float Salary;
    public int OpenPositions;
    public List<string> Requirements;
    
    public JobPosting(int companyEntityId, int officeEntityId, string position, float salary, int openPositions, List<string>? requirements = null)
    {
        CompanyEntityId = companyEntityId;
        OfficeEntityId = officeEntityId;
        Position = position;
        Salary = salary;
        OpenPositions = openPositions;
        Requirements = requirements ?? new List<string>();
    }
}
