namespace GeneLife.Core.Objectives
{
    public struct Eat : IObjective
    {
        public int Priority { get; set; }
        public string Name { get; init; }

        public Eat(int Priority, string Name = "Eat")
        {
            this.Priority = Priority;
            this.Name = Name;
        }
    }
}