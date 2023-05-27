using GeneLife.Athena.Core.Objectives;

namespace GeneLife.Athena.Components;

public struct Objectives
{
    public IObjective[] CurrentObjectives;

    public Objectives()
    {
        CurrentObjectives = new IObjective[8];
    }
}