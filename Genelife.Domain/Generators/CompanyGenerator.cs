using Genelife.Domain.Work;

namespace Genelife.Domain.Generators;

public static class CompanyGenerator
{
    private static readonly Random Random = new();
    
    private static readonly string[] TechCompanyNames =
    [
      "InnovateTech", "CodeCraft Solutions", "DataFlow Systems", "CloudVision Corp",
        "ByteForge Industries", "QuantumSoft", "NeuralLink Technologies", "CyberCore Labs"
    ];
    
    private static readonly string[] ManufacturingCompanyNames =
    [
      "SteelWorks Industries", "Precision Manufacturing", "AutoParts Corp", "MetalCraft Ltd",
        "Industrial Solutions", "MachineWorks", "Assembly Line Inc", "Production Plus"
    ];
    
    private static readonly string[] ServiceCompanyNames =
    [
      "ConsultPro Services", "Business Solutions", "Expert Advisors", "ServiceFirst",
        "Professional Partners", "Quality Consulting", "Strategic Services", "Elite Solutions"
    ];
    
    private static readonly string[] RetailCompanyNames =
    [
      "ShopSmart", "Retail Express", "Consumer Choice", "Market Leaders",
        "Shopping Central", "Retail Solutions", "Customer First", "Value Mart"
    ];
    
    private static readonly string[] HealthcareCompanyNames = [
        "HealthCare Plus", "Medical Solutions", "Wellness Corp", "LifeCare Systems",
        "Health Innovations", "Medical Excellence", "Care Providers", "Health First"
    ];

    public static Company Generate(CompanyType? companyType = null)
    {
        var type = companyType ?? (CompanyType)Random.Next(0, 5);
        
        var name = type switch
        {
            CompanyType.Technology => TechCompanyNames[Random.Next(TechCompanyNames.Length)],
            CompanyType.Manufacturing => ManufacturingCompanyNames[Random.Next(ManufacturingCompanyNames.Length)],
            CompanyType.Services => ServiceCompanyNames[Random.Next(ServiceCompanyNames.Length)],
            CompanyType.Retail => RetailCompanyNames[Random.Next(RetailCompanyNames.Length)],
            CompanyType.Healthcare => HealthcareCompanyNames[Random.Next(HealthcareCompanyNames.Length)],
            _ => "Generic Company"
        };
        
        var baseRevenue = type switch
        {
            CompanyType.Technology => Random.Next(50000, 200000),
            CompanyType.Manufacturing => Random.Next(30000, 150000),
            CompanyType.Services => Random.Next(40000, 120000),
            CompanyType.Retail => Random.Next(25000, 100000),
            CompanyType.Healthcare => Random.Next(60000, 250000),
            _ => Random.Next(30000, 100000)
        };
        
        var taxRate = Convert.ToSingle(Random.NextDouble() * 0.15 + 0.15); // 15% to 30%
        
        var minEmployees = type switch
        {
            CompanyType.Technology => Random.Next(3, 8),
            CompanyType.Manufacturing => Random.Next(5, 12),
            CompanyType.Services => Random.Next(2, 6),
            CompanyType.Retail => Random.Next(4, 10),
            CompanyType.Healthcare => Random.Next(3, 8),
            _ => Random.Next(3, 8)
        };
        
        var maxEmployees = minEmployees + Random.Next(10, 30);
        
        return new Company(
            Name: name,
            Revenue: baseRevenue,
            TaxRate: taxRate,
            EmployeeIds: new List<Guid>(),
            Type: type,
            MinEmployees: minEmployees,
            MaxEmployees: maxEmployees
        );
    }
}