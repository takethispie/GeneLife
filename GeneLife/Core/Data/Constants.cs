namespace GeneLife.Core.Data;

public static class Constants
{
    public static int MillisecondsPerTick = 1000;
    
    // a day is 5 minutes
    public static int TicksPerDay = 300;
     
    public static int TicksForAYear = 365 * TicksPerDay;

    public static int TicksUntilDeath = TicksForAYear * 80;

    public static int ChildToTeenagerTickCount = TicksForAYear * 12;

    public static int TeenagerToAdultTickCount = TicksForAYear * 25;

    public static int AdultToElderTickCOunt = TicksForAYear * 65;

    public static int HumanBodyInCubicCentimeters = 50000;

    public static float LearningMultiplier = 0.1f;

    public static int MaxAttractionComputePerLoveInterestLoop = 5;

    public static float GettingHobbyChances = 0.27f;

    public static float HobbyChangeChances = 0.1f;

    public static int HappinessLossOnHobbyLoss = 1;
    
    public static float NoHobbyReplacementChances = 0.2f;

    public static string CurrencyLabel = "$";

    public static int MaxHunger = 20, MaxThirst = 10;

    public static int HungryThreshold = 3, ThirstyThreshold = 3;
    
    public static float MaxWalkingDistance = 2000f;
}