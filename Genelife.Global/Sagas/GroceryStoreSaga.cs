using Genelife.Global.Messages.Commands.Grocery;
using Genelife.Global.Messages.Commands.Locomotion;
using Genelife.Global.Messages.Events.Buildings;
using Genelife.Global.Messages.Events.Grocery;
using Genelife.Global.Messages.Events.Locomotion;
using Genelife.Global.Sagas.States;
using Genelife.Life.Messages.Commands;
using MassTransit;
using Serilog;

namespace Genelife.Global.Sagas;

public class GroceryStoreSaga : MassTransitStateMachine<GroceryStoreSagaState>
{
    public State Active { get; set; } = null!;

    public Event<GroceryStoreBuilt> Created { get; set; } = null!;
    public Event<GoToGroceryStore> CustomerEntered { get; set; } = null!;
    public Event<LeaveGroceryStore> CustomerLeft { get; set; } = null!;
    public Event<BuyFood> BuyFood { get; set; } = null!;
    public Event<BuyDrink> BuyDrink { get; set; } = null!;
    public Event<DiscoverGroceryStores> DiscoverGroceryStores { get; set; } = null!;

    public GroceryStoreSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => CustomerEntered,
            e => e.CorrelateById(
                saga => saga.CorrelationId,
                x => x.Message.GroceryStoreId)
        );
        Event(() => CustomerLeft,
            e => e.CorrelateById(
                saga => saga.CorrelationId,
                ctx => ctx.Message.GroceryStoreId
            )
        );
        Event(() => BuyFood,
            e => e.CorrelateById(
                saga => saga.CorrelationId,
                ctx => ctx.Message.GroceryStoreId
            )
        );
        Event(() => BuyDrink,
            e => e.CorrelateById(
                saga => saga.CorrelationId,
                ctx => ctx.Message.GroceryStoreId
            )
        );
        Event(() => DiscoverGroceryStores,
            e => e.CorrelateBy(saga => "any", _ => "any")
        );

        Initially(When(Created).Then(bc =>
        {
            bc.Saga.Position = new(bc.Message.X, bc.Message.Y, bc.Message.Z);
            bc.Saga.FoodPrice = bc.Message.FoodPrice;
            bc.Saga.DrinkPrice = bc.Message.DrinkPrice;
            bc.Saga.IsOpen = true;
            bc.Saga.Revenue = 0;
            bc.Publish(new GroceryStoreAddressAnnounced(
                bc.Saga.CorrelationId,
                bc.Message.X, bc.Message.Y, bc.Message.Z
            ));
            Log.Information("Grocery store {Name} created at position ({X}, {Y}, {Z}) with food price {FoodPrice:C} and drink price {DrinkPrice:C}",
                bc.Message.Name, bc.Message.X, bc.Message.Y, bc.Message.Z, bc.Message.FoodPrice, bc.Message.DrinkPrice);
        }).TransitionTo(Active));

        During(Active,
            When(CustomerEntered).Then(bc =>
            {
                if (bc.Saga.Customers.Contains(bc.Message.CorrelationId)) return;
                bc.Saga.Customers.Add(bc.Message.CorrelationId);
                bc.Publish(new EnteredGroceryStore(bc.Message.CorrelationId, bc.Saga.CorrelationId));
                Log.Information("Customer {CustomerId} entered grocery store {StoreId}",
                    bc.Message.CorrelationId, bc.Saga.CorrelationId);
            }),
            When(CustomerLeft).Then(bc =>
            {
                if (!bc.Saga.Customers.Contains(bc.Message.CorrelationId)) return;
                bc.Saga.Customers.Remove(bc.Message.CorrelationId);
                bc.Publish(new LeftGroceryStore(bc.Message.CorrelationId, bc.Saga.CorrelationId));
                Log.Information("Customer {CustomerId} left grocery store {StoreId}",
                    bc.Message.CorrelationId, bc.Saga.CorrelationId);
            }),
            When(BuyFood).Then(bc =>
            {
                if (!bc.Saga.Customers.Contains(bc.Message.CorrelationId)) return;
                var price = (float)bc.Saga.FoodPrice;
                bc.Saga.Revenue += bc.Saga.FoodPrice;
                bc.Publish(new AddMoney(bc.Message.CorrelationId, -price));
                bc.Publish(new FoodPurchased(bc.Message.CorrelationId, bc.Saga.CorrelationId, bc.Saga.FoodPrice));
                Log.Information("Customer {CustomerId} bought food for {Price:C} at grocery store {StoreId}",
                    bc.Message.CorrelationId, price, bc.Saga.CorrelationId);
            }),
            When(BuyDrink).Then(bc =>
            {
                if (!bc.Saga.Customers.Contains(bc.Message.CorrelationId)) return;
                var price = (float)bc.Saga.DrinkPrice;
                bc.Saga.Revenue += bc.Saga.DrinkPrice;
                bc.Publish(new AddMoney(bc.Message.CorrelationId, -price));
                bc.Publish(new DrinkPurchased(bc.Message.CorrelationId, bc.Saga.CorrelationId, bc.Saga.DrinkPrice));
                Log.Information("Customer {CustomerId} bought drink for {Price:C} at grocery store {StoreId}",
                    bc.Message.CorrelationId, price, bc.Saga.CorrelationId);
            }),
            When(DiscoverGroceryStores).Then(bc =>
            {
                bc.Publish(new AddGroceryStoreAddress(
                    bc.Message.HumanId,
                    bc.Saga.CorrelationId,
                    bc.Saga.Position.X,
                    bc.Saga.Position.Y,
                    bc.Saga.Position.Z
                ));
                Log.Information("Grocery store {StoreId} responded to discovery request from human {HumanId}",
                    bc.Saga.CorrelationId, bc.Message.HumanId);
            })
        );
    }
}