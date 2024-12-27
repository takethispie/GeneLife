using Genelife.Domain.Human;
using Genelife.Domain.Items;

namespace Genelife.Domain.Housing;

public class FurnitureItem : Item
{
    public float Area { get; } // in square meters
    public Dictionary<NeedType, float> ComfortModifiers { get; }
    public bool RequiresPower { get; }
    public float PowerUsage { get; } 

    public FurnitureItem(
        string name,
        float value,
        ItemQuality quality,
        float area,
        bool requiresPower = false,
        float powerUsage = 0) 
        : base(name, ItemCategory.Furniture, value, quality)
    {
        Area = area;
        ComfortModifiers = new Dictionary<NeedType, float>();
        RequiresPower = requiresPower;
        PowerUsage = powerUsage;
    }
}