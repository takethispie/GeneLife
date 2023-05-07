namespace GeneLife.Data;

public static class Constants
{
    // a day is 5 minutes
    public static int MillisecondsPerTick = 300000;
     
    //8760
    public static int TicksForAYear = 365 * 24;

    public static int TicksUntilDeath = TicksForAYear * 79;

    public static int ChildToTeenagerTickCount = TicksForAYear * 12;

    public static int TeenagerToAdultTickCount = TicksForAYear * 25;

    public static int AdultToElderTickCOunt = TicksForAYear * 65;

}