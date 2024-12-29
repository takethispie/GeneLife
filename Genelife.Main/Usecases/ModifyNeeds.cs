using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Usecases;

public class ModifyNeeds(int hygiene, int energy, int hunger) : IMutatorUsecase<Human> {
    public int Hygiene { get; set; } = hygiene;
    public int Energy { get; set; } = energy;
    public int Hunger { get; set; } = hunger;
    
    private static float Modify(float value, float amount) => Math.Clamp(value + amount, 0f, 100f);

    public Human Execute(Human human) => human with {
        Hygiene = Modify(human.Hygiene, Hygiene),
        Energy = Modify(human.Energy, Energy),
        Hunger = Modify(human.Hunger, Hunger),
    };
}