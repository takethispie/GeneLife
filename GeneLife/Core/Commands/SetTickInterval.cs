namespace GeneLife.Core.Commands;

public class SetTickInterval : ICommand
{
    public int Interval { get; set; }

    public SetTickInterval(int interval) => Interval = interval;
}