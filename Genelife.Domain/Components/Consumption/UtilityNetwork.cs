using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Consumption;

public enum UtilityService : byte { Water, Electricity }

public enum ServiceLevel : byte { Full, Reduced, CutOff }

public enum UtilityNetworkState : byte { Normal, Strained, Overloaded, Offline }

/// <summary>
/// V5a uses exactly one authoritative network per service. Production and
/// capacity are integer units; load and reserve are derived monthly.
/// </summary>
public record struct UtilityNetwork
{
    public UtilityService Service;
    public int Production;
    public int Capacity;
    public int Load;
    public int Reserve;
    public UtilityNetworkState State;
}

public record struct BuildingServiceLink
{
    public Entity Network;
    public UtilityService Service;
    public int Demand;
    public bool Connected;
    public int Priority;
}

public record struct HouseholdMeter
{
    public int WaterUsage;
    public int ElectricityUsage;
    public ServiceLevel WaterLevel;
    public ServiceLevel ElectricityLevel;
    public Money Arrears;
    public long LastReadingTick;
    public int ConsecutivePoorMonths;
    public long LastCutOffTick;
}

public record struct ServiceLinkOwner
{
    public Entity Building;
}
