using Bogus.DataSets;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Generators;

public static class HumanGenerator {
    public static Human Build(Sex sex, int age = 18) {
        var nameGenerator = new Name();
        var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
        
        return new(
            nameGenerator.FirstName(gender),
            nameGenerator.LastName(gender),
            DateTime.Now.AddYears(-age),
            sex,
            new SkillSet(),
            20000
        );
    }
}