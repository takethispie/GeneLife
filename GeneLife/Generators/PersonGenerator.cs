using Bogus.DataSets;
using GeneLife.Data;
using GeneLife.Entities.Person;
using GeneLife.Environment;
using GeneLife.Genetic.Data;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Mutators;
using GeneLife.Utils;

namespace GeneLife.Generators;

public static class PersonGenerator
{
    public static IPerson CreatePure(Sex sex, int startAge = 0)
    {
        var random = new Random();
        var nameGenerator = new Name();
        var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
        var name = nameGenerator.FirstName(gender);
        var lastName = nameGenerator.LastName(gender);
        var age = random.Next(startAge, Constants.TicksUntilDeath - Constants.TicksForAYear * 2);
        var behavior = Enum.GetValues<BehaviorPropension>().Random(random);
        var eyeColor = Enum.GetValues<EyeColor>().Random(random);
        var hairColor = Enum.GetValues<HairColor>().Random(random);
        var handedness = Enum.GetValues<Handedness>().Random(random);
        var heightPotential = Enum.GetValues<HeightPotential>().Random(random);
        var intel = Enum.GetValues<Intelligence>().Random(random);
        var morpho = Enum.GetValues<Morphotype>().Random(random);
        var genome = new Genome(age, sex, eyeColor, hairColor, handedness, morpho, intel, heightPotential, behavior, "");
        var sequence = GenomeSequencer.ToSequence(genome);
        var id = Guid.NewGuid();
        var p = new Child(id, name, lastName, genome with { Sequence = sequence }, new EnvironmentalTraits());
        return AgeMutator.MutateAbsolute(p);
    }
}