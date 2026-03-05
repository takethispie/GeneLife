namespace Genelife.Domain.Work.Skills;

public class SkillSet {
    public List<BusinessSkill> BusinessSkills { get; set; } = new();
    public List<CommonSkill> CommonSkills { get; set; } = new();
    public List<HealthcareSkill> HealthcareSkills { get; set; } = new();
    public List<ManufacturingSkill> ManufacturingSkills { get; set; } = new();
    public List<RetailSkill> RetailSkills { get; set; } = new();
    public List<TechnicalSkill> TechnicalSkills { get; set; } = new();
    
    public int Count => BusinessSkills.Count 
                        + CommonSkills.Count 
                        + HealthcareSkills.Count 
                        + ManufacturingSkills.Count 
                        + RetailSkills.Count 
                        + TechnicalSkills.Count;

    public bool HasMinimumRequirements(SkillSet requiredSkills) {
        return false;
    }
}