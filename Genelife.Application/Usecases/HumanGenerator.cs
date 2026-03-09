using Bogus.DataSets;
using Genelife.Domain;
using Genelife.Domain.Human;

namespace Genelife.Application.Usecases;

public static class HumanGenerator {
    public static Person Build(Sex sex, int age = 18) {
        var nameGenerator = new Name();
        var gender = sex == Sex.Male ? Name.Gender.Male : Name.Gender.Female;
        
        return new(
            Guid.NewGuid(),
            nameGenerator.FirstName(gender),
            nameGenerator.LastName(gender),
            DateTime.Now.AddYears(-age),
            sex,
            new LifeSkillSet(),
            new Position(0, 0, 0),
            20000
        );
    }
}