namespace GeneLife.Data;

public static class Constants
{
    public static int MillisecondsPerTick = 1000;
    
    // a day is 5 minutes
    public static int TickPerDay = 300;
     
    public static int TicksForAYear = 365 * TickPerDay;

    public static int TicksUntilDeath = TicksForAYear * 80;

    public static int ChildToTeenagerTickCount = TicksForAYear * 12;

    public static int TeenagerToAdultTickCount = TicksForAYear * 25;

    public static int AdultToElderTickCOunt = TicksForAYear * 65;

    public static int HumanBodyInCubicCentimeters = 50000;

    public static float LearningMultiplier = 0.1f;
}