using Genelife.Domain;

namespace Genelife.Inventory;

public record Item(int Id, string Name, ItemType ItemType);