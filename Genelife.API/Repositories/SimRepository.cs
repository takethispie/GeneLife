using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;

namespace Genelife.Api.Repositories;

public class SimRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext? economy;

    public SimRepository(World world, NetworkIdRegistry networkIds, EconomyContext? economy = null)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
    }

    public NetworkId Add(string name, int age)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f),
            new SkillProfile(),
            new Health(Genelife.Systems.HealthSystem.InitialForAge(age)),
            LifecycleFactory.Create(age, economy?.Tick ?? 0, economy?.Clock ?? SimulationClock.Default, economy?.Life.Policy)
        );

        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        economy?.Life.Index.OnBorn((economy?.Life.Policy ?? LifeStagePolicy.Default).StageFor(age));
        return networkId;
    }

    public NetworkId Add(string name, int age, string playerId)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new PlayerControlled(playerId),
            new Location(0f, 0f),
            new SkillProfile(),
            new Health(Genelife.Systems.HealthSystem.InitialForAge(age)),
            LifecycleFactory.Create(age, economy?.Tick ?? 0, economy?.Clock ?? SimulationClock.Default, economy?.Life.Policy)
        );

        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        economy?.Life.Index.OnBorn((economy?.Life.Policy ?? LifeStagePolicy.Default).StageFor(age));
        return networkId;
    }

    public bool Delete(ulong networkId)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity))
            return false;

        if (!world.IsAlive(entity))
            return false;

        if (economy is not null && world.Has<Lifecycle>(entity))
        {
            economy.Life.Index.OnDied(world.Get<Lifecycle>(entity).Stage);
            Genelife.Systems.EnrollmentHelpers.ReleaseSeat(world, entity, economy.Life.Index);
        }

        networkIds.Remove(new NetworkId(networkId));
        world.Destroy(entity);
        return true;
    }

    public bool SetAction(ulong networkId, string actionType)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity))
            throw new InvalidOperationException($"Sim with NetworkId {networkId} not found");

        if (!world.IsAlive(entity))
            throw new InvalidOperationException($"Sim with NetworkId {networkId} is no longer alive");

        if (!Enum.TryParse<ActionType>(actionType, ignoreCase: true, out var action))
            throw new InvalidOperationException($"Unknown action type: {actionType}");

        if (world.Has<CurrentAction>(entity))
        {
            ref var currentAction = ref world.Get<CurrentAction>(entity);
            currentAction = new CurrentAction(action, 0f);
        }
        else
        {
            world.Add(entity, new CurrentAction(action, 0f));
        }

        return true;
    }

    public List<SimStatus> GetAll()
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();

        world.Query(in query, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId networkId) =>
        {
            var isPlayerControlled = world.Has<PlayerControlled>(entity);
            sims.Add(new SimStatus
            {
                NetworkId = networkId.Value,
                Name = name.Name,
                Age = world.TryGet<Alive>(entity, out var alive) ? alive.Age : 0,
                IsPlayerControlled = isPlayerControlled,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                TimeRemaining = action.TimeRemaining,
                Stage = world.Has<Lifecycle>(entity)
                    ? world.Get<Lifecycle>(entity).Stage.ToString()
                    : LifecycleClassifier.Classify(world.TryGet<Alive>(entity, out var ageOnly) ? ageOnly.Age : 0).ToString(),
                Health = world.Has<Health>(entity) ? (decimal)world.Get<Health>(entity).Value : 0m,
                TopSkill = world.Has<SkillProfile>(entity) ? TopSkill(world.Get<SkillProfile>(entity)) : string.Empty,
                Seniority = world.Has<Employee>(entity) ? world.Get<Employee>(entity).Seniority.ToString() : string.Empty,
                IsDead = world.Has<Dead>(entity)
            });
        });

        return sims;
    }

    private static string TopSkill(SkillProfile profile)
    {
        var best = string.Empty;
        var value = 0f;
        foreach (var skill in SkillProfile.Families)
        {
            var proficiency = profile.Get(skill);
            if (proficiency > value)
            {
                value = proficiency;
                best = skill.ToString();
            }
        }
        return best;
    }
}
