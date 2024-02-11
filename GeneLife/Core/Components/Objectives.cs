using GeneLife.Core.ObjectiveActions;

namespace GeneLife.Core.Components
{
    public struct Objectives
    {
        public IObjective[] CurrentObjectives;
        public int TickWithNoObjectives;

        public Objectives()
        {
            CurrentObjectives = new IObjective[] {
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
            new EmptyObjective(),
        };
            TickWithNoObjectives = 0;
        }

        public bool IsHighestPriority(Type objectiveType)
        {
            if (CurrentObjectives == null) return false;
            var top = CurrentObjectives.MaxBy(x => x.Priority);
            return top != null && top.GetType() == objectiveType;
        }

        public void SetNewHighestPriority(IObjective objective)
        {
            CurrentObjectives = CurrentObjectives.Select(x =>
            {
                if (x.Priority == 10) x.Priority = 9;
                return x;
            }).Prepend(objective).Take(CurrentObjectives.Length - 1).ToArray();
        }

        public void RemoveHighestPriority()
        {
            var newPriorities = CurrentObjectives.Where(x => x.Priority != 10);
            var hasNewTop = false;
            CurrentObjectives = newPriorities.Select(x =>
            {
                if (x.Priority == 9 && !hasNewTop)
                {
                    hasNewTop = true;
                    x.Priority = 10;
                }
                return x;
            }).Append(new EmptyObjective()).ToArray();
        }
    }
}