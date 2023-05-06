using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities;

public record Neighbourhood(
    int id, 
    int RegionId, 
    string Name, 
    SocialEnvironment LocalSocialEnvironment, 
    IEnumerable<OriginSpread> OriginSpread, 
    int AverageHousingPrices
);