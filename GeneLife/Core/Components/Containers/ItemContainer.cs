namespace GeneLife.Core.Components.Containers
{
    public struct ItemContainer
    {
        public int MaxAmount;
        public int CurrentAmount;

        /// <summary>
        /// ids of the entities stored in the container
        /// </summary>
        public int[] Content;

        public ItemContainer(int currentAmount, int maxAmount)
        {
            CurrentAmount = currentAmount;
            MaxAmount = maxAmount;
            Content = new int[maxAmount];
        }
    }
}