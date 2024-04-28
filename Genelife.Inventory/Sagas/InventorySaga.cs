using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Inventory.Sagas;

public class InventorySaga : 
    ISaga, 
    InitiatedBy<CreateHuman>, 
    InitiatedBy<CreateGroceryShop>, 
    Orchestrates<StoreItem>, 
    Orchestrates<TakeItem>, 
    Orchestrates<BuyItem>,
    ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public List<Item> Items { get; set; }
    public int Money { get; set; }
    public int Version { get; set; }

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Items = [];
        Money = 1000;
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<CreateGroceryShop> context)
    {
        Items = [];
        Money = 1000000;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<StoreItem> context)
    {
        Items.Add(ItemMapper.Map(context.Message.ItemId));
        await context.Publish(new ItemStored(CorrelationId));
    }

    public async Task Consume(ConsumeContext<TakeItem> context)
    {
        var found = false;
        Items = Items.Where((x, idx) => {
            if(x.ItemType == context.Message.ItemType && found is false) {
                found = true;
                return false;
            }
            return true;
        }).ToList();
        await context.Publish(found ? 
            new ItemFound(CorrelationId, context.Message.ItemType) 
            : new ItemNotFound(CorrelationId, context.Message.ItemType));
    }

    public async Task Consume(ConsumeContext<BuyItem> context)
    {
        Money -= ItemPriceMapper.Map(context.Message.ItemType);
        Items.Add(ItemMapper.Map(context.Message.ItemType));
        Console.WriteLine($"new balance for {CorrelationId}: {Money}C");
        await context.Publish(new ItemBought(CorrelationId, context.Message.ItemType));
    }
}