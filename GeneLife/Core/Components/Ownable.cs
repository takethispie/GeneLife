namespace GeneLife.Core.Components;

public struct Ownable
{
    public int OwnerId;

    public Ownable(int ownerId)
    {
        OwnerId = ownerId;
    }
}