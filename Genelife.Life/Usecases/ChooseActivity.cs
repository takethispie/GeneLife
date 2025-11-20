using Genelife.Life.Domain.Activities;
using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Usecases;

public class ChooseActivity {
    public ILivingActivity? Execute(Human human, int hour, bool works) {
        List<(float val, string name)> actions = [];
        if (hour is >= 22 or <= 1 && human.Energy < 25) 
            actions.Add((human.Energy, "Energy"));
        if(hour is > 5 and < 8 or > 18 and < 22 && human.Hygiene < 25) 
            actions.Add((human.Hygiene, "Hygiene"));
        if(hour is > 12 and < 14 or > 18 and < 22 && human.Hunger < 30) 
            actions.Add((human.Hunger, "Hunger"));
        if(hour is > 7 and < 18 && human.Energy > 50 && works)
            actions.Add((human.Energy, "Work"));
        
        if (actions.Count == 0) return null;
        
        return actions.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Shower(),
            "Hunger" => new Eat(),
            "Work" => new Domain.Activities.Work(),
            _ => null
        };
    }
}