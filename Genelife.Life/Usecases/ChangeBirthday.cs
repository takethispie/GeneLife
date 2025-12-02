using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Usecases;

public class ChangeBirthday {
    public Human Execute(Human entity, int age) => entity with { Birthday = DateTime.Now.AddYears(-age) };
}