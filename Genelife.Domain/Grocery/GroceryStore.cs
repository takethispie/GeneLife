namespace Genelife.Domain.Grocery;

public class GroceryStore(Guid id, Position position, int foodPrice, int drinkPrice)
{
    public Guid Id {get; init; } = id;
    public Position Position { get; private set; } = position;
    public List<Guid> Customers { get; private set; } = [];
    public int FoodPrice { get; private set; } = foodPrice;
    public int DrinkPrice { get; private set; } = drinkPrice;
    public float Revenue { get; private set; } = 0;

    public void CustomerEnters(Guid customerId)
    {
        if (Customers.Contains(customerId)) return;
        Customers.Add(customerId);
    }
    
    public void CustomerLeaves(Guid customerId)
    {
        if (!Customers.Contains(customerId)) return;
        Customers.Remove(customerId);
    }

    public bool BuyDrink(Guid customerId, int count = 1)
    {
        if (!Customers.Contains(customerId)) return false;
        Revenue += DrinkPrice  * count;
        return true;
    }

    public bool BuyFood(Guid customerId, int count = 1)
    {
        if (!Customers.Contains(customerId)) return false;
        Revenue += FoodPrice * count;
        return true;
    }
}