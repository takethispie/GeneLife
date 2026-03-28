using Genelife.Domain;

namespace Genelife.API.DTOs;

public record CreateHumanRequest(
    string FirstName,
    string LastName,
    int Age,
    Sex Sex,
    float Money
);