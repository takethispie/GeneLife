namespace GeneLife.Core.Commands;

public class SetTicksPerDayCommand : ICommand
{
    public int Ticks { get; set; }

    public SetTicksPerDayCommand(int ticks) => Ticks = ticks;
}