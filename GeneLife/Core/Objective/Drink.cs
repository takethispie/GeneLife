namespace GeneLife.Core.Objective
{
    public struct Drink : IObjective
    {
        public int Priority { get; set; }
        public string Name { get; init; }

        public Drink(int Priority, string Name = "Drink")
        {
            this.Priority = Priority;
            this.Name = Name;
        }
    }
}