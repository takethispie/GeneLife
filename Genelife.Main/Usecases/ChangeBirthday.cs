using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Usecases;

public class ChangeBirthday {
    public Human Execute(Human entity, int age) => entity with { Birthday = DateTime.Now.AddYears(-age) };
}