using Bogus.DataSets;
using Genelife.Domain.Human;

namespace Genelife.Domain.Generators;

public static class HumanGenerator {
    public static Character Build(Sex sex, int age = 18) {
        var nameGenerator = new Name();
        var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
        return new(Guid.NewGuid(), nameGenerator.FirstName(gender), nameGenerator.LastName(gender), age, sex);
    }
}