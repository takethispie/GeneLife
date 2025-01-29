using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Usecases;

public class UpdateNeeds {
    private static float Modify(float value, float amount) => Math.Clamp(value + amount, 0f, 100f);

    private static float Decay(float value, float decayRate) => Modify(value, -decayRate * 60);

    public Human Execute(Human human) =>
        human with {
            Hunger = Decay(human.Hunger, 0.05f),
            Energy = Decay(human.Energy, 0.03f),
            Hygiene = Decay(human.Hygiene, 0.04f),
        };
}