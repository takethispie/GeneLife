namespace GeneLife.Core.Components.Buildings;

public struct HouseHold
{
    public int[] Members;

    public HouseHold()
    {
        Members = Array.Empty<int>();
    }

    public HouseHold(int[] members)
    {
        Members = members;
    }
}