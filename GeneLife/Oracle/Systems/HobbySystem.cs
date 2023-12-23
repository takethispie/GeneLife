using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Events;
using GeneLife.Core.Extensions;
using GeneLife.Genetic;
using GeneLife.Oracle.Components;
using GeneLife.Oracle.Core;
using GeneLife.Oracle.Services;

namespace GeneLife.Oracle.Systems;

internal sealed class HobbySystem : BaseSystem<World, float>
{
    private readonly float _interval;
    private float _currentTimeCount;
    private readonly QueryDescription withoutHobby = new QueryDescription().WithAll<Living, Genome, Identity>().WithNone<Hobby>();
    private readonly QueryDescription withHobby = new QueryDescription().WithAll<Living, Genome, Hobby, Identity, Psychology>();
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
            var identity = entity.Get<Identity>();
            if (_random.Next() <= Constants.HobbyChangeChances)
            {
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
            }
            else
            {
                switch (hobby.NeedsMoney)
                {
                    case true when entity.Has<Wallet>():
                        var wallet = entity.Get<Wallet>();
                        if (wallet.Money < hobby.MoneyPerWeek)
                        {
                            var psyc = entity.Get<Psychology>();
                            var newEmBal = psyc.EmotionalBalance -= Constants.HappinessLossOnHobbyLoss;
                            entity.Set(psyc with { EmotionalBalance = newEmBal });
                            NotEnoughMoneyLog(identity, hobby);
                            entity.Remove<Hobby>();
                        }
                        else
                        {
                            entity.Set(new Wallet { Money = wallet.Money -= hobby.MoneyPerWeek });
                            UseMoneyForHobbyLog(identity, wallet);
                        }
                        break;

                    case true when !entity.Has<Wallet>():
                        entity.Remove<Hobby>();
                        break;
                }
            }
        });

        World.Query(in withoutHobby, (ref Entity entity) =>
        {
            if (_random.Next() > Constants.GettingHobbyChances) return;
            var newHobby = Enum.GetValues<HobbyType>().Random(_random);
            var (needMoney, amount) = HobbyService.GetHobbyExpenses(newHobby);
            entity.Add(new Hobby
                { Type = newHobby, Started = DateTime.Now, NeedsMoney = needMoney, MoneyPerWeek = amount });
            var identity = entity.Get<Identity>();
            NewHobbyLog(identity, newHobby);
            //TODO maybe use money to fund hobby as soon as you get it
        });
    }

    private static void ChangedHobbyLog(Identity identity, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{identity.FirstName} {identity.LastName} has changed hobby interest to {hobby.Type}"
        });
    }

    private static void NewHobbyLog(Identity identity, HobbyType newHobby)
    {
        EventBus.Send(new LogEvent { Message = $"{identity.FirstName} {identity.LastName} Got a new hobby: {newHobby}" });
    }

    private static void NotInterestedAnymoreLog(Identity identity, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{identity.FirstName} {identity.LastName} is not interested in {hobby.Type} anymore"
        });
    }

    private static void NotEnoughMoneyLog(Identity identity, Hobby hobby)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{identity.FirstName} {identity.LastName} doesnt have enough money for {hobby.Type}" +
                      $" {identity.FirstName} is a bit less happy"
        });
    }

    private static void UseMoneyForHobbyLog(Identity identity, Wallet wallet)
    {
        EventBus.Send(new LogEvent
        {
            Message = $"{identity.FirstName} {identity.LastName} used {wallet.Money}{Constants.CurrencyLabel} " +
                      $"to fund his/her hobby"
        });
    }
}