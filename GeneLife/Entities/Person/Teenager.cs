using GeneLife.Data;
using GeneLife.Environment;

namespace GeneLife.Entities.Person;

public record Teenager(int Id, string Name, string LastName, Genome Genome, EnvironmentalTraits Traits) : IPerson;