using GeneLife.Athena.Core.Objectives;

public struct EmptyObjective : IObjective {
    public int Priority { get; set; }
    public string Name { get; init; }
    
    public EmptyObjective()
    {
        this.Priority = -1;
        this.Name = "None";
    }
}