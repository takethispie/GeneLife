using System.Numerics;

namespace GeneLife.Athena.Core.Objectives;

public struct MoveTo : IObjective
{
    public int Priority { get; set; }
    public Vector3 target { get; init; }
    public string Name { get; init; }
    
    public MoveTo(int Priority, Vector3 target, string Name = "Move To")
    {
        this.Priority = Priority;
        this.target = target;
        this.Name = Name;
    }
}