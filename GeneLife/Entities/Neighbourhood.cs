using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities;

public record Neighbourhood(
    Guid Id, 
    Guid RegionId,
    string Name,
    SocialEnvironment LocalSocialEnvironment, 
    int AverageBuyingPrice,
    int AverageRentingPrice
) : IEntity;