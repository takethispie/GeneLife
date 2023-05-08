using GeneLife.Environment;

namespace GeneLife.Data.Person;

public class Person
{
    public Person(Guid id, int age, string name, string lastName, string genome, EnvironmentalTraits traits)
    {
        Id = id;
        Age = age;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Genome = genome ?? throw new ArgumentNullException(nameof(genome));
        Traits = traits ?? throw new ArgumentNullException(nameof(traits));
        LastName = lastName;
    }

    public Guid Id { get; init; }
    public int Age { get; }
    public string Name { get; init; }
    public string LastName { get; init; }
    public string Genome { get; init; }
    public EnvironmentalTraits Traits { get; init; }
}