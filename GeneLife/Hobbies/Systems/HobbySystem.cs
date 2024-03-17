using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Data;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Genetic;
using GeneLife.Hobbies.Components;
using GeneLife.Hobbies.Services;
using GeneLife.Survival.Components;

namespace GeneLife.Hobbies.Systems;

internal sealed class HobbySystem : BaseSystem<World, float>
{
    private readonly float _interval;
    private float _currentTimeCount;
    private readonly QueryDescription withoutHobby = new QueryDescription().WithAll<Living, Genome, Human>().WithNone<Hobby>();
    private readonly QueryDescription withHobby = new QueryDescription().WithAll<Living, Genome, Hobby, Human>();
    private readonly Random _random;

    public HobbySystem(World world) : base(world)
    {
        _interval = Constants.TicksPerDay * 7;
        _currentTimeCount = 0;
        _random = new Random();
    }

    public override void Update(in float delta)
    {
        _currentTimeCount += delta;
        if (_currentTimeCount < _interval) return;
        World.Query(in withHobby, (ref Entity entity) =>
        {
            var hobby = entity.Get<Hobby>();
            var identity = entity.Get<Human>();
            if (_random.Next() <= Constants.HobbyChangeChances)
                if (_random.Next() <= Constants.NoHobbyReplacementChances)
                {
                    NotInterestedAnymoreLog(identity, hobby);
                    entity.Remove<Hobby>();
                }
                else
                {
                    var newHobby = Enum.GetValues<HobbyType>().Random(_random);
                    var (needMoney, amount) = HobbyService.GetHobbyExpenses(newHobby);
                    entity.Set(new Hobby
                    { Type = newHobby, Started = DateTime.Now, NeedsMoney = needMoney, MoneyPerWeek = amount });
                    ChangedHobbyLog(identity, hobby);
                }
            else
                switch (hobby.NeedsMoney)
                {
                    case true when entity.Has<Human>():
                        var human = entity.Get<Human>();
                        if (human.Money < hobby.MoneyPerWeek)
                        {
                            var newEmBal = human.EmotionalBalance -= Constants.HappinessLossOnHobbyLoss;
                            entity.Set(human with { EmotionalBalance = newEmBal });
                            NotEnoughMoneyLog(identity, hobby);
                            entity.Remove<Hobby>();
                        }
                        else
                        {
                            entity.Set(human with { Money = human.Money -= hobby.MoneyPerWeek });
                            UseMoneyForHobbyLog(human);
                        }
                        break;
                }
        });

        World.Query(in withoutHobby, (ref Entity entity) =>
        {
            if (_random.Next() > Constants.GettingHobbyChances) return;
            var newHobby = Enum.GetValues<HobbyType>().Random(_random);
            var (needMoney, amount) = HobbyService.GetHobbyExpenses(newHobby);
            entity.Add(new Hobby
            { Type = newHobby, Started = DateTime.Now, NeedsMoney = needMoney, MoneyPerWeek = amount });
            var identity = entity.Get<Human>();
            NewHobbyLog(identity, newHobby);
            //TODO maybe use money to fund hobby as soon as you get it
        });
    }

    private static void ChangedHobbyLog(Human human, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{human.FirstName} {human.LastName} has changed hobby interest to {hobby.Type}"
        });
    }

    private static void NewHobbyLog(Human human, HobbyType newHobby)
    {
        EventBus.Send(new LogEvent { Message = $"{human.FirstName} {human.LastName} Got a new hobby: {newHobby}" });
    }

    private static void NotInterestedAnymoreLog(Human human, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{human.FirstName} {human.LastName} is not interested in {hobby.Type} anymore"
        });
    }

    private static void NotEnoughMoneyLog(Human human, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{human.FirstName} {human.LastName} doesnt have enough money for {hobby.Type}" +
                      $" {human.FirstName} is a bit less happy"
        });
    }

    private static void UseMoneyForHobbyLog(Human human)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{human.FirstName} {human.LastName} used {human.Money}{Constants.CurrencyLabel} " +
                      $"to fund his/her hobby"
        });
    }
}