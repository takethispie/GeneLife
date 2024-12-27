using System.Numerics;
using Genelife.Domain;
using Genelife.Domain.Commands;
using Genelife.Domain.Commands.Buildings;
using Genelife.Domain.Commands.Cheat;
using Genelife.Domain.Commands.Create;
using Genelife.Domain.Commands.Items;
using Genelife.Domain.Commands.Money;
using Genelife.Domain.Events;
using Genelife.Domain.Events.Buildings;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Items;
using Genelife.Domain.Events.Movement;
using Genelife.Domain.Items;
using MassTransit;
using Serilog;

namespace Genelife.Main.Sagas;

public class HumanSaga : MassTransitStateMachine<HumanSagaState>
{
    public State Idle { get; set; } = null!;
    public State Busy { get; set; } = null!;
    public State Moving { get; set; } = null;
    public State Working { get; set; } = null;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<DayElapsed> DayElapsed { get; set; } = null;
    public Event<ClosestGroceryShopFound> FoundGroceryShop { get; set; } = null;
    public Event<ItemsBought> ItemsBought { get; set; } = null;
    public Event<TransferHourlyPay> MoneyTransfered { get; set;} = null;


    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            
        }).TransitionTo(Idle));
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));
        
        
    }
}