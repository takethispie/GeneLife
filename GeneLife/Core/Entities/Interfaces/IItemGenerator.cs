using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Interfaces;

public interface IItemGenerator
{
    
    /// <summary>
    /// Get a full list of all available item, id 0 should never be used
    /// </summary>
    /// <returns>array of all the available items available in game</returns>
    public Item[] GetItemList();
}