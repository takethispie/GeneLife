using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Application.Generators;

public static class CompanyGenerator
{
    private static readonly Random random = new();
    
    private static readonly string[] techCompanyNames =
    [
      "InnovateTech", "CodeCraft Solutions", "DataFlow Systems", "CloudVision Corp",
        "ByteForge Industries", "QuantumSoft", "NeuralLink Technologies", "CyberCore Labs"
    ];
    
    private static readonly string[] manufacturingCompanyNames =
    [
      "SteelWorks Industries", "Precision Manufacturing", "AutoParts Corp", "MetalCraft Ltd",
        "Industrial Solutions", "MachineWorks", "Assembly Line Inc", "Production Plus"
    ];
    
    private static readonly string[] serviceCompanyNames =
    [
      "ConsultPro Services", "Business Solutions", "Expert Advisors", "ServiceFirst",
        "Professional Partners", "Quality Consulting", "Strategic Services", "Elite Solutions"
    ];
    
    private static readonly string[] retailCompanyNames =
    [
      "ShopSmart", "Retail Express", "Consumer Choice", "Market Leaders",
        "Shopping Central", "Retail Solutions", "Customer First", "Value Mart"
    ];
    
    private static readonly string[] healthcareCompanyNames = [
        "HealthCare Plus", "Medical Solutions", "Wellness Corp", "LifeCare Systems",
        "Health Innovations", "Medical Excellence", "Care Providers", "Health First"
    ];

    public static Company Generate(CompanyType? companyType = null)
    {
        var type = companyType ?? (CompanyType)random.Next(0, 5);
        
        var name = type switch
        {
            CompanyType.Technology => techCompanyNames[random.Next(techCompanyNames.Length)],
            CompanyType.Manufacturing => manufacturingCompanyNames[random.Next(manufacturingCompanyNames.Length)],
            CompanyType.Services => serviceCompanyNames[random.Next(serviceCompanyNames.Length)],
            CompanyType.Retail => retailCompanyNames[random.Next(retailCompanyNames.Length)],
            CompanyType.Healthcare => healthcareCompanyNames[random.Next(healthcareCompanyNames.Length)],
            _ => "Generic Company"
        };
        
        var baseRevenue = type switch
        {
            CompanyType.Technology => random.Next(50000, 200000),
            CompanyType.Manufacturing => random.Next(30000, 150000),
            CompanyType.Services => random.Next(40000, 120000),
            CompanyType.Retail => random.Next(25000, 100000),
            CompanyType.Healthcare => random.Next(60000, 250000),
            _ => random.Next(30000, 100000)
        };

        var taxRate = Convert.ToSingle(random.NextDouble() * 0.15 + 0.10);
        
        var minEmployees = type switch
        {
            CompanyType.Technology => random.Next(3, 8),
            CompanyType.Manufacturing => random.Next(5, 12),
            CompanyType.Services => random.Next(2, 6),
            CompanyType.Retail => random.Next(4, 10),
            _ => random.Next(3, 8)
        };
        
        var maxEmployees = minEmployees + random.Next(10, 30);
        
        return new Company(
            Name: name,
            Revenue: baseRevenue,
            TaxRate: taxRate,
            EmployeeIds: new List<Guid>(),
            Type: type,
            AverageProductivity: 0f
        );
    }
}