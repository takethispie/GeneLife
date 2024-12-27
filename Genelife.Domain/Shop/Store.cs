using Genelife.Domain.Human;
using Genelife.Domain.Items;

namespace Genelife.Domain.Shop;

public class Store
{
    public string Name { get; }
    public StoreType Type { get; }
    public List<StoreItem> Inventory { get; private set; }
    public Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> BusinessHours { get; }
    public float CashBalance { get; private set; }
    private Random random = new Random();
    private List<StoreEmployee> employees;
    private Dictionary<DayOfWeek, List<StoreEmployee>> schedule = new();
    public IReadOnlyList<StoreEmployee> Employees => employees.AsReadOnly();

    public Store(string name, StoreType type, float initialCash = 5000)
    {
        Name = name;
        Type = type;
        CashBalance = initialCash;
        Inventory = [];
        BusinessHours = InitializeBusinessHours();
        RestockInventory();
        employees = [];
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek))) {
            schedule[day] = [];
        }
    }
    

    private Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)> InitializeBusinessHours()
    {
        var hours = new Dictionary<DayOfWeek, (TimeSpan Open, TimeSpan Close)>();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek))) {
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
        switch (Type) {
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
        AddStoreItem(ItemFactory.CreateFood("Apple", 1, 10f), 50);
        AddStoreItem(ItemFactory.CreateFood("Sandwich", 5, 25f), 20);
        AddStoreItem(ItemFactory.CreateFood("Pizza", 15, 50f), 10);
        AddStoreItem(ItemFactory.CreateFood("Salad", 8, 30f), 15);
        AddStoreItem(ItemFactory.CreateFood("Energy Drink", 3, 5f), 30);
    }
    

    private void AddFurnitureItems()
    {
        AddStoreItem(ItemFactory.CreateFurniture("Basic Chair", 50, ItemQuality.Normal), 5);
        AddStoreItem(ItemFactory.CreateFurniture("Luxury Sofa", 500, ItemQuality.Excellent), 2);
        AddStoreItem(ItemFactory.CreateFurniture("Dining Table", 200, ItemQuality.Good), 3);
        AddStoreItem(ItemFactory.CreateFurniture("Bed", 300, ItemQuality.Good), 3);
    }
    

    private void AddElectronicsItems()
    {
        var tv = new Item("Television", ItemCategory.Electronics, 800, ItemQuality.Good, durability: 1000);
        tv.UseEffects[NeedType.Fun] = 20f;
        AddStoreItem(tv, 3);

        var computer = new Item("Computer", ItemCategory.Electronics, 1200, ItemQuality.Excellent, durability: 1000);
        computer.UseEffects[NeedType.Fun] = 25f;
        AddStoreItem(computer, 2);
    }
    

    private void AddBookItems()
    {
        AddStoreItem(ItemFactory.CreateBook("Cooking Guide", 25), 10);
        AddStoreItem(ItemFactory.CreateBook("Programming Manual", 35), 8);
        AddStoreItem(ItemFactory.CreateBook("Fiction Novel", 15), 15);
        AddStoreItem(ItemFactory.CreateBook("Self-Help Book", 20), 12);
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
        
        foreach (var item in Inventory.Where(i => i.Quantity > 0)) {
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
        var totalCost = storeItem.Item.Value * quantity;
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
        var buybackPrice = customerItem.Value * 0.5f * quantity; // 50% of original value
        if (CashBalance < buybackPrice)
            return (false, "Store cannot afford to buy items");
        if (!customer.Inventory.RemoveItem(itemName, quantity))
            return (false, "Failed to remove items from customer inventory");
        // Transaction successful
        customer.Money += buybackPrice;
        CashBalance -= buybackPrice;
        return (true, $"Successfully sold {quantity} {itemName}(s) to store");
    }
    
    
    public bool HireEmployee(Character character, int shiftStart)
    {
        if (employees.Any(e => e.Character == character))
            return false;

        var position = new StorePosition(
            this,
            $"{Type} Staff",
            15, // Starting hourly wage
            new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            shiftStart,
            8
        );

        var employee = new StoreEmployee(character, position);
        employees.Add(employee);

        // Add to schedule
        foreach (var day in position.ShiftDays) {
            schedule[day].Add(employee);
        }
        return true;
    }
    

    public bool FireEmployee(Character character)
    {
        var employee = employees.FirstOrDefault(e => e.Character == character);
        if (employee == null)
            return false;
        employees.Remove(employee);
        foreach (var daySchedule in schedule.Values) {
            daySchedule.Remove(employee);
        }
        return true;
    }
    

    public void UpdateEmployees(DateTime currentTime)
    {
        foreach (var employee in employees)
        {
            if (employee.Position.IsScheduledToWork(currentTime)) {
                employee.StartShift(currentTime);
                
                // Random customer interactions during shift
                if (random.NextDouble() < 0.3) {
                    float saleAmount = random.Next(10, 100);
                    employee.ServeCostumer(saleAmount);
                }
            }
            else employee.EndShift(currentTime);

            // Check for promotion
            if (!(random.NextDouble() < 0.1)) continue; // 10% chance per update
            if (employee.Position.TryPromote(employee.Character.Skills))
                employee.Character.Money += 100; // Promotion bonus
        }
    }
    

    public string GetEmployeeReport()
    {
        var report = $"Employee Report for {Name}\n";
        report += $"Total Employees: {employees.Count}\n\n";

        foreach (var employee in employees) {
            report += $"Employee: {employee.Character.FirstName} {employee.Character.LastName}\n";
            report += $"Position: {employee.Position.Name} (Level {employee.Position.Level})\n";
            report += $"Performance: {employee.Performance:P0}\n";
            report += $"Last Shift: {employee.LastShiftDate.ToShortDateString()}\n";
            report += $"Sales Total: ${employee.SalesTotal:F2}\n";
            report += $"Customers Served: {employee.CustomersServed}\n\n";
        }
        return report;
    }

    
    public string GetScheduleReport()
    {
        var report = "Weekly Schedule:\n";
        foreach (var day in schedule)
        {
            report += $"\n{day.Key}:\n";
            if (day.Value.Count != 0) {
                foreach (var employee in day.Value) {
                    report += $"  {employee.Character.FirstName} {employee.Character.LastName}: " +
                              $"{employee.Position.ShiftStart:hh\\:mm} - " +
                             $"{employee.Position.ShiftStart + employee.Position.ShiftLength:hh\\:mm}\n";
                }
            }
            else report += "  No employees scheduled\n";
        }
        return report;
    }
}