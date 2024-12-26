using Genelife.Domain.Human;
using Genelife.Domain.Items;

namespace Genelife.Domain.Store;

public class Store
{
    public string Name { get; }
    public StoreType Type { get; }
    public List<StoreItem> Inventory { get; private set; }
    public Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> BusinessHours { get; }
    public decimal CashBalance { get; private set; }
    private Random random = new Random();

    public Store(string name, StoreType type, decimal initialCash = 5000m)
    {
        Name = name;
        Type = type;
        CashBalance = initialCash;
        Inventory = new List<StoreItem>();
        BusinessHours = InitializeBusinessHours();
        RestockInventory();
    }

    private Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> InitializeBusinessHours()
    {
        var hours = new Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)>();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            if (day == DayOfWeek.Sunday)
                hours[day] = (new TimeSpan(10, 0, 0), new TimeSpan(18, 0, 0));
            else
                hours[day] = (new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0));
        }
        return hours;
    }

    public bool IsOpen(DateTime currentTime)
    {
        if (!BusinessHours.TryGetValue(currentTime.DayOfWeek, out var hours))
            return false;

        var currentTimeOfDay = currentTime.TimeOfDay;
        return currentTimeOfDay >= hours.Open && currentTimeOfDay < hours.Close;
    }

    public void RestockInventory()
    {
        Inventory.Clear();
        switch (Type)
        {
            case StoreType.Grocery:
                AddGroceryItems();
                break;
            case StoreType.Furniture:
                AddFurnitureItems();
                break;
            case StoreType.Electronics:
                AddElectronicsItems();
                break;
            case StoreType.Bookstore:
                AddBookItems();
                break;
        }
    }

    private void AddGroceryItems()
    {
        AddStoreItem(ItemFactory.CreateFood("Apple", 1m, 10f), 50);
        AddStoreItem(ItemFactory.CreateFood("Sandwich", 5m, 25f), 20);
        AddStoreItem(ItemFactory.CreateFood("Pizza", 15m, 50f), 10);
        AddStoreItem(ItemFactory.CreateFood("Salad", 8m, 30f), 15);
        AddStoreItem(ItemFactory.CreateFood("Energy Drink", 3m, 5f), 30);
    }

    private void AddFurnitureItems()
    {
        AddStoreItem(ItemFactory.CreateFurniture("Basic Chair", 50m, ItemQuality.Normal), 5);
        AddStoreItem(ItemFactory.CreateFurniture("Luxury Sofa", 500m, ItemQuality.Excellent), 2);
        AddStoreItem(ItemFactory.CreateFurniture("Dining Table", 200m, ItemQuality.Good), 3);
        AddStoreItem(ItemFactory.CreateFurniture("Bed", 300m, ItemQuality.Good), 3);
    }

    private void AddElectronicsItems()
    {
        var tv = new Item("Television", ItemCategory.Electronics, 800m, ItemQuality.Good, durability: 1000);
        tv.UseEffects[NeedType.Fun] = 20f;
        AddStoreItem(tv, 3);

        var computer = new Item("Computer", ItemCategory.Electronics, 1200m, ItemQuality.Excellent, durability: 1000);
        computer.UseEffects[NeedType.Fun] = 25f;
        AddStoreItem(computer, 2);
    }

    private void AddBookItems()
    {
        AddStoreItem(ItemFactory.CreateBook("Cooking Guide", 25m), 10);
        AddStoreItem(ItemFactory.CreateBook("Programming Manual", 35m), 8);
        AddStoreItem(ItemFactory.CreateBook("Fiction Novel", 15m), 15);
        AddStoreItem(ItemFactory.CreateBook("Self-Help Book", 20m), 12);
    }

    private void AddStoreItem(Item item, int quantity)
    {
        Inventory.Add(new StoreItem(item, quantity));
    }

    public string GetStoreReport()
    {
        var report = $"Store Report for {Name} ({Type})\n";
        report += $"Cash Balance: ${CashBalance:F2}\n";
        report += "Inventory:\n";
        
        foreach (var item in Inventory.Where(i => i.Quantity > 0))
        {
            report += $"  {item.Item.Name} - ${item.Item.Value:F2} (x{item.Quantity})\n";
        }
        
        return report;
    }

    public (bool success, string message) SellItemToCustomer(Character customer, string itemName, int quantity = 1)
    {
        var storeItem = Inventory.FirstOrDefault(i => i.Item.Name == itemName);
        
        if (storeItem == null)
            return (false, "Item not in stock");
            
        if (storeItem.Quantity < quantity)
            return (false, "Not enough items in stock");
            
        decimal totalCost = storeItem.Item.Value * quantity;
        
        if (customer.Money < totalCost)
            return (false, "Customer cannot afford item");

        if (!customer.Inventory.AddItem(storeItem.Item, quantity))
            return (false, "Customer inventory is full");

        // Transaction successful
        customer.Money -= totalCost;
        CashBalance += totalCost;
        storeItem.Quantity -= quantity;

        return (true, $"Successfully purchased {quantity} {itemName}(s)");
    }

    public (bool success, string message) BuyItemFromCustomer(Character customer, string itemName, int quantity = 1)
    {
        var customerItem = customer.Inventory.FindItem(itemName);
        if (customerItem == null)
            return (false, "Customer doesn't have this item");

        decimal buybackPrice = customerItem.Value * 0.5m * quantity; // 50% of original value
        
        if (CashBalance < buybackPrice)
            return (false, "Store cannot afford to buy items");

        if (!customer.Inventory.RemoveItem(itemName, quantity))
            return (false, "Failed to remove items from customer inventory");

        // Transaction successful
        customer.Money += buybackPrice;
        CashBalance -= buybackPrice;

        return (true, $"Successfully sold {quantity} {itemName}(s) to store");
    }
}