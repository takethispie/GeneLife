using Genelife.Global.Messages.DTOs;
using MassTransit;

namespace Genelife.Global.Sagas.States;

public class GroceryStoreSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Position Position { get; set; } = new(0, 0, 0);
    public List<Guid> Customers { get; set; } = [];
    public decimal FoodPrice { get; set; } = 5.00m;
    public decimal DrinkPrice { get; set; } = 3.00m;
    public decimal Revenue { get; set; } = 0;
    public bool IsOpen { get; set; } = true;
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;
}