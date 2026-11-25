namespace Genelife.Map;

public static class BuildingFootprints
{
    public static Footprint ForProperty(int units, bool isShelter = false)
    {
        if (isShelter)
            return new Footprint(4, 3);
        var width = 4 + Math.Clamp(units / 10, 0, 4);
        var height = 3 + Math.Clamp(units / 20, 0, 2);
        return new Footprint(width, height);
    }

    public static Footprint ForOffice(int capacity)
    {
        var width = 4 + Math.Clamp(capacity / 25, 0, 3);
        var height = 3 + Math.Clamp(capacity / 50, 0, 2);
        return new Footprint(width, height);
    }

    public static Footprint ForSchool(int capacity)
    {
        var width = 5 + Math.Clamp(capacity / 100, 0, 3);
        var height = 4 + Math.Clamp(capacity / 200, 0, 2);
        return new Footprint(width, height);
    }
}
