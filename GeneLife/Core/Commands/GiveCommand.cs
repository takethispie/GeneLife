using GeneLife.Core.Items;

namespace GeneLife.Core.Commands
{
    public class GiveCommand : ICommand
    {
        public Item Item { get; init; }
        public string TargetFirstName { get; init; }
        public string TargetLastName { get; init; }
    }
}