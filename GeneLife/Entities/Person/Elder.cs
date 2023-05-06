using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities.Person;

public record Elder(int Id, string Name, string LastName, Genome Genome, EnvironmentalTraits Traits) : IPerson;