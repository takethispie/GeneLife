using GeneLife.Environment;

namespace GeneLife.Entities;

public record Country(
    int Id, 
    string Name, 
    IEnumerable<Country> Neighbours, 
    int HappinessRatio, 
    int BaseRevenue, 
    ClimateType Climate, 
    SocialEnvironment OverallSocialEnvironment
);
