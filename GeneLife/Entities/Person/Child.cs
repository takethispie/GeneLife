using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities.Person;

public record Child(Guid Id, string Name, string LastName, Genome Genome, EnvironmentalTraits Traits) : IPerson;