namespace GeneLife.Core.ObjectiveActions
{
    public struct BuyItem : IObjective
    {
        public int Priority { get; set; }
        public int ItemId { get; init; }
        public string Name { get; init; }

        public BuyItem(int Priority, int ItemId, string Name = "BuyItem")
        {
            this.Priority = Priority;
            this.ItemId = ItemId;
            this.Name = Name;
        }
    }
}