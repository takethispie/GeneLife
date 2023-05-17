using GeneLife.Core;
using GeneLife.Core.Objectives;

namespace GeneLife.Common.Components;

public struct Objectives
{
    public IObjective[] CurrentObjectives;

    public Objectives()
    {
        CurrentObjectives = new IObjective[8];
    }
}