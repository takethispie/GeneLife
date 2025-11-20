using Genelife.Domain.Work.Skills;

namespace Genelife.Domain;

public record Human(
    string FirstName,
    string LastName,
    DateTime Birthday,
    Sex Sex,
    SkillSet SkillSet,
    float Money,
    float Hunger = 100,
    float Energy = 100,
    float Hygiene = 100
);