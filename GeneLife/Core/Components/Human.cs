namespace GeneLife.Core.Components;
public struct Human
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int EmotionalBalance { get; set; }
    public float Money { get; set; }

    public readonly string FullName() => $"{FirstName} {LastName}";
}
