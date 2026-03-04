using Genelife.Domain;

namespace Genelife.Application.Usecases;

public class ChangeBirthday {
    public Human Execute(Human entity, int age) => entity with { Birthday = DateTime.Now.AddYears(-age) };
}