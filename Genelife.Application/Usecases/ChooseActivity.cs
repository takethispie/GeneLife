using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;
using Genelife.Domain.Human.Activities;

namespace Genelife.Application.Usecases;

public class ChooseActivity {
    public IBeingActivity Execute(Person person, int hour, bool works) {
        List<(float val, string name)> actions = [];
        if (hour is >= 22 or <= 1 && person.Energy < 25)
            actions.Add((person.Energy, "Energy"));
        if(hour is > 5 and < 8 or > 18 and < 22 && person.Hygiene < 25)
            actions.Add((person.Hygiene, "Hygiene"));
        if(hour is > 12 and < 14 or > 18 and < 22 && person.Hunger < 30)
            actions.Add((person.Hunger, "Hunger"));
        if(hour is > 10 and < 16 or > 19 and < 23 && person.Thirst < 30)
            actions.Add((person.Thirst, "Thirst"));
        if(hour is > 7 and < 18 && person.Energy > 50 && works)
            actions.Add((person.Energy, "Work"));
        
        if (actions.Count == 0) return new Idle();
        
        return actions.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Shower(),
            "Hunger" => new Eat(),
            "Thirst" => new Drink(),
            "Work" => new Work(),
            _ => new Idle()
        };
    }
}