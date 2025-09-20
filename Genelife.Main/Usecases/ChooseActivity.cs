using Genelife.Domain;
using Genelife.Domain.Interfaces;
using Genelife.Main.Domain.Activities;

namespace Genelife.Main.Usecases;

public class ChooseActivity {
    public ILivingActivity? Execute(Human human, int hour) {
        List<(float val, string name)> actions = [];
        if (hour is >= 22 or <= 1 && human.Energy < 25) 
            actions.Add((human.Energy, "Energy"));
        if(hour is > 5 and < 8 or > 18 and < 22 && human.Hygiene < 25) 
            actions.Add((human.Hygiene, "Hygiene"));
        if(hour is > 12 and < 14 or > 18 and < 22 && human.Hunger < 30) 
            actions.Add((human.Hunger, "Hunger"));
        if(hour is > 7 and < 18 && human.Energy > 50)
            actions.Add((human.Energy, "Work"));
        
        if (actions.Count == 0) return null;
        
        return actions.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Shower(),
            "Hunger" => new Eat(),
            "Work" => new Work(),
            _ => null
        };
    }
}