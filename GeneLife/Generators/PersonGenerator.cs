using Bogus.DataSets;
using GeneLife.Data;
using GeneLife.Entities.Person;
using GeneLife.Environment;
using GeneLife.GeneticTraits;
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
        var age = random.Next(startAge, Constant.TicksUntilDeath - Constant.TicksForAYear * 2);
        var behavior = Enum.GetValues<BehaviorPropension>().Random(random);
        var eyeColor = Enum.GetValues<EyeColor>().Random(random);
        var hairColor = Enum.GetValues<HairColor>().Random(random);
        var handedness = Enum.GetValues<Handedness>().Random(random);
        var heightPotential = Enum.GetValues<HeightPotential>().Random(random);
        var intel = Enum.GetValues<Intelligence>().Random(random);
        var morpho = Enum.GetValues<Morphotype>().Random(random);
        var genome = new Genome(age, sex, eyeColor, hairColor, handedness, morpho, intel, heightPotential, behavior);
        var id = (GenomeSequencer.ToSequence(genome) + name + lastName + DateTime.Now.Ticks).GetHashCode();
        var p = new Child(id, name, lastName, genome, new EnvironmentalTraits());
        return AgeMutator.MutateAbsolute(p);
    }
    
    public static IPerson MakeChild(IPerson Father, IPerson mother)
    {
        var FGamete = MeiosisMutator.BuildGamete(Father.Genome);
        var MGamete = MeiosisMutator.BuildGamete(mother.Genome);
        var gen = GeneticMergingMutator.ProduceZygote(FGamete, MGamete);
        var name = new Name().FirstName();
        //in 80% of cases child gets father's name
        var lastName = new Random().NextSingle() >= 0.8 ? mother.LastName : Father.LastName;
        var id = (GenomeSequencer.ToSequence(gen) + name + lastName + DateTime.Now.Ticks).GetHashCode();
        return new Child(id, name, lastName, gen, new EnvironmentalTraits());
    }
}