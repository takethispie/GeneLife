namespace Genelife.Life.Messages.DTOs;

public record Human(
    string FirstName,
    string LastName,
    DateTime Birthday,
    Sex Sex,
    LifeSkillSet LifeSkillSet,
    Coordinates Coordinates,
    float Money,
    float Hunger = 100,
    float Energy = 100,
    float Hygiene = 100
);