using Arch.Core;
using Arch.Core.Extensions;
using Bogus.DataSets;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Extensions;
using GeneLife.Genetic;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Knowledge.Components;
using GeneLife.Survival.Components;
using System.Numerics;

namespace GeneLife.Core.Entities.Generators
{
    public static class PersonGenerator
    {
        public static Entity CreatePure(World world, Sex sex, int startAge = 0, int endAge = 80, float money = 0)
        {
            var random = new Random();
            var nameGenerator = new Name();
            var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
            var identity = new Human { Id = Guid.NewGuid(), FirstName = nameGenerator.FirstName(gender), LastName = nameGenerator.LastName(gender), Money = money };
            var age = random.Next(startAge, endAge);
            var behavior = Enum.GetValues<BehaviorPropension>().Random(random);
            var eyeColor = Enum.GetValues<EyeColor>().Random(random);
            var hairColor = Enum.GetValues<HairColor>().Random(random);
            var handedness = Enum.GetValues<Handedness>().Random(random);
            var heightPotential = Enum.GetValues<HeightPotential>().Random(random);
            var intel = Enum.GetValues<Intelligence>().Random(random);
            var morpho = Enum.GetValues<Morphotype>().Random(random);
            var gen = new Genome(age, sex, eyeColor, hairColor, handedness, morpho, intel, heightPotential, behavior);
            var entity = world.Create(new NpcArchetypeFactory().Build("Person"));
            entity.Set(identity);
            entity.Set(gen);
            entity.Set(new KnowledgeList());
            entity.Set(new Living());
            entity.Set(new Inventory());
            entity.Set(new Objectives());
            entity.Set(new Position(Vector3.Zero));
            return entity;
        }
    }
}