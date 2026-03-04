using System.Numerics;
using Bogus.DataSets;
using Genelife.Domain;

namespace Genelife.Application.Generators;

public static class HumanGenerator {
    public static Human Build(Sex sex, int age = 18) {
        var nameGenerator = new Name();
        var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
        
        return new(
            nameGenerator.FirstName(gender),
            nameGenerator.LastName(gender),
            DateTime.Now.AddYears(-age),
            sex,
            new LifeSkillSet(),
            new Coordinates(0, 0, 0),
            20000
        );
    }
}