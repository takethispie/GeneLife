using GeneLife.Core;

namespace GeneLife.Common.Components;

public struct Objectives
{
    public IObjective[] CurrentObjectives;

    public Objectives()
    {
        CurrentObjectives = new IObjective[8];
    }
}