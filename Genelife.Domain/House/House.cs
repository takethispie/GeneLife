namespace Genelife.Domain.House;

public sealed class House(Guid id, Position position, List<Guid> owners)
{
    public Guid Id { get; init; } = id;
    public Position Position { get; private set; } = position;
    public List<Guid> Owners { get; private set; } = owners;
    public List<Guid> Occupants { get; private set; } = [];
    
    public void OccupantsEnters(Guid customerId)
    {
        if (Occupants.Contains(customerId)) return;
        Occupants.Add(customerId);
    }
    
    public void OccupantsLeaves(Guid customerId)
    {
        if (!Occupants.Contains(customerId)) return;
        Occupants.Remove(customerId);
    }
}