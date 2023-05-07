using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities.Person;

public interface IPerson
{
    int Id { get; init; }
    string Name { get; init; }
    string LastName { get; init; }
    Genome Genome { get; init; }
    EnvironmentalTraits Traits { get; init; }
}