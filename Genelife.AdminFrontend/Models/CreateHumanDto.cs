using Genelife.Domain;

namespace Genelife.AdminFrontend.Models;

public sealed class CreateHumanDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 25;
    public Sex Sex { get; set; } = Sex.Male;
    public float Money { get; set; } = 20000f;
}