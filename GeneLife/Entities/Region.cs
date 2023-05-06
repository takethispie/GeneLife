using GeneLife.Environment;

namespace GeneLife.Entities;

public record Region(int Id, int CountryId, string Name, SocialEnvironment SocialEnvironment, int RevenueDelta);
