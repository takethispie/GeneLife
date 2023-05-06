namespace GeneLife.Data;

public static class Constant
{
    public static int MillisecondsPerTick = 1000;
     
    //8760
    public static int TicksForAYear = 365 * 24;

    public static int TicksUntilDeath = TicksForAYear * 79;

    public static int ChildToTeenagerTickCount = TicksForAYear * 12;

    public static int TeenagerToAdultTickCount = TicksForAYear * 25;

    public static int AdultToElderTickCOunt = TicksForAYear * 65;

}