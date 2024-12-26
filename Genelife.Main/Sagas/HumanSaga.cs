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
    public State Moving { get; set; } = null;
    public State GroceryStoreLoop { get; set; } = null;
    public State Working { get; set; } = null;

    public Event<CreateHuman> Created { get; set; } = null;
    public Event<Arrived> Arrived { get; set; } = null;
    public Event<Tick> UpdateTick { get; set; } = null;
    public Event<DayElapsed> DayElapsed { get; set; } = null;
    public Event<ClosestGroceryShopFound> FoundGroceryShop { get; set; } = null;
    public Event<ItemsBought> ItemsBought { get; set; } = null;
    public Event<SetHunger> SetHunger { get; set; } = null;
    public Event<SetThirst> SetThirst { get; set; } = null;
    public Event<TransferHourlyPay> MoneyTransfered { get; set;} = null;


    public HumanSaga() {
        InstanceState(x => x.CurrentState);
        Initially(When(Created).Then(bc => {
            bc.Saga.Hunger = bc.Message.Hunger;
            bc.Saga.Thirst = bc.Message.Thirst;
            bc.Saga.Position = new(bc.Message.X, bc.Message.Y, 0);
            bc.Saga.Home = bc.Saga.Position;
        }).TransitionTo(Idle));
        Event(() => UpdateTick, e => e.CorrelateBy(saga => "any", ctx => "any"));
        Event(() => DayElapsed, e => e.CorrelateBy(saga => "any", ctx => "any"));

        DuringAny(
            When(DayElapsed).Then((bc) => {
                Log.Information($"{bc.Saga.CorrelationId} Hunger: {bc.Saga.Hunger} Thirst: {bc.Saga.Thirst}");
                bc.Saga.Hunger++;
                bc.Saga.Thirst++;
            }),

            When(SetHunger).Then(bc => bc.Saga.Hunger = bc.Message.Value),
            When(SetThirst).Then(bc => bc.Saga.Thirst = bc.Message.Value),
            When(MoneyTransfered).Then(bc =>
            {
                Log.Information($"{bc.Message.Amount} added to {bc.CorrelationId}, new balance: {bc.Saga.Money}");
                bc.Saga.Money += bc.Message.Amount;
            })
        );
    }
}