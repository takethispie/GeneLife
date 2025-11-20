namespace Genelife.Work.Messages.DTOs.Skills;

public static class SkillExtensions
{
    public static string ToDisplayString(this CommonSkill skill) => skill switch
    {
        CommonSkill.Communication => "Communication",
        CommonSkill.ProblemSolving => "Problem Solving",
        CommonSkill.TeamWork => "Team Work",
        CommonSkill.Leadership => "Leadership",
        CommonSkill.TimeManagement => "Time Management",
        CommonSkill.CriticalThinking => "Critical Thinking",
        CommonSkill.Adaptability => "Adaptability",
        CommonSkill.Creativity => "Creativity",
        CommonSkill.CustomerService => "Customer Service",
        CommonSkill.ProjectManagement => "Project Management",
        CommonSkill.DataAnalysis => "Data Analysis",
        CommonSkill.Research => "Research",
        CommonSkill.MicrosoftOffice => "Microsoft Office",
        CommonSkill.EmailManagement => "Email Management",
        CommonSkill.PresentationSkills => "Presentation Skills",
        CommonSkill.Organization => "Organization",
        _ => skill.ToString()
    };

    public static string ToDisplayString(this TechnicalSkill skill) => skill switch
    {
        TechnicalSkill.CSharp => "C#",
        TechnicalSkill.JavaScript => "JavaScript",
        TechnicalSkill.Python => "Python",
        TechnicalSkill.React => "React",
        TechnicalSkill.Angular => "Angular",
        TechnicalSkill.Docker => "Docker",
        TechnicalSkill.Kubernetes => "Kubernetes",
        TechnicalSkill.AWS => "AWS",
        TechnicalSkill.Azure => "Azure",
        TechnicalSkill.SQL => "SQL",
        TechnicalSkill.NoSQL => "NoSQL",
        TechnicalSkill.Git => "Git",
        TechnicalSkill.Agile => "Agile",
        TechnicalSkill.Scrum => "Scrum",
        TechnicalSkill.RestAPIs => "REST APIs",
        TechnicalSkill.Microservices => "Microservices",
        TechnicalSkill.CICD => "CI/CD",
        TechnicalSkill.UnitTesting => "Unit Testing",
        TechnicalSkill.HTML => "HTML",
        TechnicalSkill.CSS => "CSS",
        TechnicalSkill.NodeJs => "Node.js",
        _ => skill.ToString()
    };

    public static string ToDisplayString(this BusinessSkill skill) => skill switch
    {
        BusinessSkill.Excel => "Excel",
        BusinessSkill.PowerPoint => "PowerPoint",
        BusinessSkill.CRMSoftware => "CRM Software",
        BusinessSkill.BusinessAnalysis => "Business Analysis",
        BusinessSkill.Negotiation => "Negotiation",
        BusinessSkill.StrategicPlanning => "Strategic Planning",
        BusinessSkill.ProcessImprovement => "Process Improvement",
        BusinessSkill.FinancialAnalysis => "Financial Analysis",
        BusinessSkill.Marketing => "Marketing",
        BusinessSkill.Sales => "Sales",
        BusinessSkill.Accounting => "Accounting",
        BusinessSkill.Budgeting => "Budgeting",
        BusinessSkill.Forecasting => "Forecasting",
        _ => skill.ToString()
    };

    public static string ToDisplayString(this HealthcareSkill skill) => skill switch
    {
        HealthcareSkill.PatientCare => "Patient Care",
        HealthcareSkill.MedicalTerminology => "Medical Terminology",
        HealthcareSkill.HIPAACompliance => "HIPAA Compliance",
        HealthcareSkill.ElectronicHealthRecords => "Electronic Health Records",
        HealthcareSkill.ClinicalSkills => "Clinical Skills",
        HealthcareSkill.MedicalEquipment => "Medical Equipment",
        HealthcareSkill.Documentation => "Documentation",
        HealthcareSkill.Pharmacology => "Pharmacology",
        HealthcareSkill.VitalSigns => "Vital Signs",
        HealthcareSkill.MedicalCoding => "Medical Coding",
        HealthcareSkill.InsuranceProcessing => "Insurance Processing",
        _ => skill.ToString()
    };

    public static string ToDisplayString(this ManufacturingSkill skill) => skill switch
    {
        ManufacturingSkill.LeanManufacturing => "Lean Manufacturing",
        ManufacturingSkill.SixSigma => "Six Sigma",
        ManufacturingSkill.QualityControl => "Quality Control",
        ManufacturingSkill.SafetyProtocols => "Safety Protocols",
        ManufacturingSkill.EquipmentMaintenance => "Equipment Maintenance",
        ManufacturingSkill.ProcessImprovement => "Process Improvement",
        ManufacturingSkill.InventoryManagement => "Inventory Management",
        ManufacturingSkill.CADSoftware => "CAD Software",
        ManufacturingSkill.StatisticalAnalysis => "Statistical Analysis",
        ManufacturingSkill.MachineOperation => "Machine Operation",
        ManufacturingSkill.Welding => "Welding",
        _ => skill.ToString()
    };

    public static string ToDisplayString(this RetailSkill skill) => skill switch
    {
        RetailSkill.POSSystems => "POS Systems",
        RetailSkill.VisualMerchandising => "Visual Merchandising",
        RetailSkill.CashHandling => "Cash Handling",
        RetailSkill.InventoryManagement => "Inventory Management",
        RetailSkill.LossPrevention => "Loss Prevention",
        RetailSkill.RetailSoftware => "Retail Software",
        RetailSkill.ProductKnowledge => "Product Knowledge",
        RetailSkill.Upselling => "Upselling",
        RetailSkill.StoreOperations => "Store Operations",
        RetailSkill.CustomerRelations => "Customer Relations",
        _ => skill.ToString()
    };

    public static T[] GetAllValues<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>();
    }
}