using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Usecases;

public class ModifyNeeds {
    private static float Modify(float value, float amount) => Math.Clamp(value + amount, 0f, 100f);

    public Human Execute(Human human, int hygiene, int energy, int hunger) => human with {
        Hygiene = Modify(human.Hygiene, hygiene),
        Energy = Modify(human.Energy, energy),
        Hunger = Modify(human.Hunger, hunger),
    };
}