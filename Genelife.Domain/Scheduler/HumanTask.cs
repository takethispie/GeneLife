namespace Genelife.Domain.Scheduler;

public class HumanTask : ISlot
{
    public string Name { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }

    public HumanTask(string name, TimeOnly startTime, TimeOnly endTime)
    {
        Name = name;
        Start = startTime;
        End = endTime;

        if (startTime >= endTime)
        {
            throw new ArgumentException("Start time must be before end time.");
        }
    }
}
