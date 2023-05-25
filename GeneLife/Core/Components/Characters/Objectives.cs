using GeneLife.Core.Objectives;

namespace GeneLife.Core.Components.Characters;

public struct Objectives
{
    public IObjective[] CurrentObjectives;

    public Objectives()
    {
        CurrentObjectives = new IObjective[8];
    }
}