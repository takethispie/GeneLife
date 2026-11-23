using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Messages.Events;

namespace Genelife.Systems;

/// <summary>
/// Monthly curriculum gain for education-stage sims. Gains depend on school
/// quality, attendance, emphasis and household background, and are deterministic
/// (no Random.Shared).
/// </summary>
public class EducationSystem : ISimulationSystem
{
    private const float BaseGain = 0.005f;
    private const float NonEmphasisShare = 0.30f;
    private const float EmphasisMultiplier = 1.6f;

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public EducationSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        query = new QueryDescription()
            .WithAll<Lifecycle, Enrollment, SkillProfile>()
            .WithNone<Dead>();
    }

    public string Name => "Education";
    public int Order => 320;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var milestones = new List<(Entity Entity, Skill Skill, float Value)>();
        var missingProfiles = new List<Entity>();

        world.Query(in query, (Entity entity, ref Lifecycle lifecycle, ref Enrollment enrollment, ref SkillProfile profile) =>
        {
            if (lifecycle.Stage != LifeStage.Education)
                return;

            var quality = 0.4f;
            var emphasis = Skill.None;
            if (!enrollment.IsUnenrolled &&
                world.IsAlive(enrollment.School) &&
                world.Has<School>(enrollment.School))
            {
                var school = world.Get<School>(enrollment.School);
                quality = school.Quality;
                emphasis = school.Emphasis;
            }

            var background = Background(entity);
            var attendance = enrollment.IsUnenrolled ? enrollment.Attendance * 0.3f : enrollment.Attendance;
            var gain = BaseGain * quality * attendance * background;
            if (gain <= 0f)
                return;

            var aptitude = world.TryGet<Traits>(entity, out var traits) ? traits.Aptitude : (Aptitude?)null;

            var emphasized = 0;
            foreach (var family in SkillProfile.Families)
            {
                var before = profile.Get(family);
                var useEmphasis = emphasis.HasFlag(family) && emphasized < 2;
                if (useEmphasis)
                    emphasized++;
                var multiplier = useEmphasis ? EmphasisMultiplier : NonEmphasisShare;
                var learning = aptitude is { } value
                    ? AptitudeSkills.LearningMultiplier(value, family)
                    : 1f;
                profile.Add(family, gain * multiplier * learning);
                var after = profile.Get(family);
                if (Crossed(before, after, 0.25f) || Crossed(before, after, 0.5f) || Crossed(before, after, 0.75f))
                    milestones.Add((entity, family, after));
            }

            world.Get<SkillProfile>(entity) = profile;
        });

        world.Query(in new QueryDescription().WithAll<Lifecycle>().WithNone<SkillProfile, Dead>(),
            (Entity entity, ref Lifecycle lifecycle) =>
            {
                if (lifecycle.Stage == LifeStage.Education)
                    missingProfiles.Add(entity);
            });

        foreach (var entity in missingProfiles)
        {
            if (world.IsAlive(entity) && !world.Has<SkillProfile>(entity))
                world.Add(entity, new SkillProfile());
        }

        foreach (var (entity, skill, value) in milestones)
        {
            economy.Emit(new SkillMilestoneReached(
                LifeSystemHelpers.NetworkIdOf(world, entity),
                skill.ToString(),
                value,
                tick));
        }
    }

    private float Background(Entity sim)
    {
        var household = LifeSystemHelpers.HouseholdOf(world, economy, sim);
        if (household == Entity.Null)
            return 0.9f;

        var earners = 0;
        foreach (var member in economy.Housing.GetMembers(household))
        {
            if (world.IsAlive(member) && world.Has<Employee>(member))
                earners++;
        }

        return earners > 0 ? 1.1f : 0.9f;
    }

    private static bool Crossed(float before, float after) =>
        Crossed(before, after, 0.25f) || Crossed(before, after, 0.5f) || Crossed(before, after, 0.75f);

    private static bool Crossed(float before, float after, float threshold) =>
        before < threshold && after >= threshold;
}
