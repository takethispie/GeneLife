using Genelife.Domain;
using Genelife.Domain.Interfaces;
using Genelife.Main.Domain.Activities;

namespace Genelife.Main.Usecases;

public class ChooseActivity : ISideEffectUsecase<IActivity, Human> {
    public IActivity Execute(Human human) {
        List<(float val, string name)> needs = [
            (human.Energy, "Energy"), 
            (human.Hygiene, "Hygiene"),
            (human.Hunger, "Hunger")
        ];
        return needs.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Shower(),
            "Hunger" => new Eat(),
            _ => null
        };
    }
}