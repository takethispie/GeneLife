namespace Genelife.Domain;

public record Human(
    string FirstName,
    string LastName,
    DateTime Birthday,
    Sex Sex,
    float Money,
    float Hunger = 100,
    float Energy = 100,
    float Hygiene = 100
);