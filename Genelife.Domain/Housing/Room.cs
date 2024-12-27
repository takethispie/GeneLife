using Genelife.Domain.Human;
using Genelife.Domain.Items;

namespace Genelife.Domain.Housing;

public class Room
{
    public string Name { get; set; }
    public RoomType Type { get; }
    public float Area { get; } // in square meters
    public List<FurnitureItem> Furniture { get; }
    public Dictionary<NeedType, float> ComfortModifiers { get; private set; }
    public Dictionary<string, float> RoomStats { get; private set; }

    public Room(string name, RoomType type, float area)
    {
        Name = name;
        Type = type;
        Area = area;
        Furniture = new List<FurnitureItem>();
        ComfortModifiers = new Dictionary<NeedType, float>();
        RoomStats = new Dictionary<string, float>
        {
            { "Cleanliness", 100f },
            { "Temperature", 21f }, // Default room temperature in Celsius
            { "Light", 100f }
        };
    }

    public bool AddFurniture(FurnitureItem item)
    {
        if (GetUsedArea() + item.Area > Area)
            return false;

        Furniture.Add(item);
        UpdateRoomModifiers();
        return true;
    }

    public bool RemoveFurniture(FurnitureItem item)
    {
        var removed = Furniture.Remove(item);
        if (removed)
            UpdateRoomModifiers();
        return removed;
    }

    public float GetUsedArea()
    {
        return Furniture.Sum(f => f.Area);
    }

    public float GetFurnitureValue()
    {
        return Furniture.Sum(f => f.Value);
    }

    public float GetQualityScore()
    {
        if (!Furniture.Any()) return 0;
        return Furniture.Average(f => (float)f.Quality) / Enum.GetValues(typeof(ItemQuality)).Length;
    }

    private void UpdateRoomModifiers()
    {
        ComfortModifiers.Clear();
        foreach (var furniture in Furniture)
        {
            foreach (var modifier in furniture.ComfortModifiers)
            {
                if (!ComfortModifiers.ContainsKey(modifier.Key))
                    ComfortModifiers[modifier.Key] = 0;
                ComfortModifiers[modifier.Key] += modifier.Value;
            }
        }
    }

    public void Update(TimeSpan timePassed)
    {
        // Degrade cleanliness over time
        RoomStats["Cleanliness"] = Math.Max(0, RoomStats["Cleanliness"] - (float)timePassed.TotalHours * 0.5f);
    }
}