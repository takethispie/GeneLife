using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using GeneLife.CommonComponents;
using GeneLife.Data;
using GeneLife.Genetic;
using GeneLife.Oracle.Components;
using GeneLife.Oracle.Core;
using GeneLife.Utils;

namespace GeneLife.Oracle.Systems;

public class HobbySystem : BaseSystem<World, float>
{
    private readonly float _interval;
    private float _currentTimeCout;
    private readonly QueryDescription withoutHobby = new QueryDescription().WithAll<Living, Genome>().WithNone<Hobby>();
    private readonly QueryDescription withHobby = new QueryDescription().WithAll<Living, Genome, Hobby>();
    private Random _random;
    
    public HobbySystem(World world) : base(world)
    {
        _interval = Constants.TickPerDay * 7;
        _currentTimeCout = 0;
        _random = new Random();
    }
    
    public override void Update(in float delta)
    {
        _currentTimeCout += delta;
        if (_currentTimeCout < _interval) return;
        World.ParallelQuery(in withHobby, (in Entity entity) =>
        {
            var hobby = entity.Get<Hobby>();
            if (_random.Next() > Constants.HobbyChangeChances)
            {
                if (_random.Next() > Constants.NoHobbyReplacementChances)
                {
                    entity.Remove<Hobby>();
                    entity.Get<Psychology>().EmotionalBalance -= Constants.HappinessLossOnHobbyLoss;
                }
                else
                {
                    var newHobby = Enum.GetValues<HobbyType>().Random(_random);
                    entity.Set(new Hobby() { Type = newHobby, Started = DateTime.Now });
                }
            }
            else
            {
                switch (hobby.NeedsMoney)
                {
                    case true when entity.Has<Wallet>():
                        var wallet = entity.Get<Wallet>();
                        if (wallet.Money < hobby.MoneyPerWeek) entity.Remove<Hobby>();
                        else wallet.Money -= hobby.MoneyPerWeek;
                        break;
                    
                    case true when !entity.Has<Wallet>():
                        entity.Remove<Hobby>();
                        break;
                }
            }
        });
        
        World.Query(in withoutHobby, (in Entity entity) =>
        {
            if (_random.Next() <= Constants.GettingHobbyChances) return;
            var newHobby = Enum.GetValues<HobbyType>().Random(_random);
            entity.Add(new Hobby { Type = newHobby, Started = DateTime.Now });
            //TODO use money to fund hobby
        });
    }
}