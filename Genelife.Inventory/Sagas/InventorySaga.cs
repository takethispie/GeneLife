using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;
using Serilog;

namespace Genelife.Inventory.Sagas;

public class InventorySaga : 
    ISaga, 
    InitiatedBy<CreateHuman>, 
    InitiatedBy<CreateGroceryShop>, 
    Orchestrates<StoreItem>, 
    Orchestrates<TakeItem>, 
    Orchestrates<BuyItems>,
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
        Log.Information($"{context.CorrelationId} stored item {context.Message.ItemId}");
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
        Log.Information($"{context.Message.CorrelationId} found and took needed item");
        await context.Publish(found ? 
            new ItemFound(CorrelationId, context.Message.ItemType) 
            : new ItemNotFound(CorrelationId, context.Message.ItemType));
    }

    public Task Consume(ConsumeContext<BuyItems> context)
    {
        context.Message.Items.ToList().ForEach(async item => {
            Money -= ItemPriceMapper.Map(item.ItemType);
            Items.Add(ItemMapper.Map(item.ItemType));
            await context.Publish(new ItemBought(CorrelationId, item.ItemType));
        });
        Log.Information($"new balance for {CorrelationId}: {Money}C");
        return Task.CompletedTask;
    }
}