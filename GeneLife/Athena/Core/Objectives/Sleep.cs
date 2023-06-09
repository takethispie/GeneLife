namespace GeneLife.Athena.Core.Objectives;

public class Sleep : IObjective
{
    public int Priority { get; set; }
    public string Name { get; init; }
    
    public Sleep(int Priority, string Name = "Sleep")
    {
        this.Priority = Priority;
        this.Name = Name;
    }
}