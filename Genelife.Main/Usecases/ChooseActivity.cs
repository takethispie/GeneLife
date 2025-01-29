using Genelife.Domain;
using Genelife.Domain.Interfaces;
using Genelife.Main.Domain.Activities;

namespace Genelife.Main.Usecases;

public class ChooseActivity {
    public ILivingActivity Execute(Human human, int hour) {
        List<(float val, string name)> needs = [];

        if (hour >= 22) needs.Add((human.Energy, "Energy"));
        if(hour is > 5 and < 8 or > 18 and < 22) needs.Add((human.Hygiene, "Hygiene"));
        if(hour is > 12 and < 14 or > 18 and < 22) needs.Add((human.Hunger, "Hunger"));
        
        return needs.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Shower(),
            "Hunger" => new Eat(),
            _ => null
        };
    }
}