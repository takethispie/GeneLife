using System.Numerics;
using Genelife.Physical.Domain;

namespace Genelife.Physical.Repository;

public class GroceryShopCache() {
    private List<GroceryShop> groceryShops = [];

    public void Add(GroceryShop groceryShop) {
        if(groceryShops.Any(x => x.Guid == groceryShop.Guid) is false) 
            groceryShops.Add(groceryShop);
    }

    public void Remove(Guid guid) => groceryShops = groceryShops.Where(x => x.Guid != guid).ToList();

    public GroceryShop? GetClosest(Vector3 position) => groceryShops.OrderByDescending(x => Vector3.Distance(x.Position, position)).FirstOrDefault();
}