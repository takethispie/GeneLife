namespace Genelife.Main.Scheduler;

internal class Week {
    public int WeekNumber { get; set; }
    public Day[] Days { get; set; } = new Day[7];
}