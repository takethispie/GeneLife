using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Life;
using Genelife.Life;

namespace Genelife.Systems;

/// <summary>
/// Single place that releases an enrollment and keeps the school count plus the
/// lifecycle index in sync. Used by graduation, death, deletion and admin edits.
/// </summary>
public static class EnrollmentHelpers
{
    public static bool ReleaseSeat(World world, Entity sim, LifecycleIndex? index)
    {
        if (!world.IsAlive(sim) || !world.Has<Enrollment>(sim))
            return false;

        var enrollment = world.Get<Enrollment>(sim);
        if (!enrollment.IsUnenrolled && world.IsAlive(enrollment.School) &&
            world.Has<School>(enrollment.School))
        {
            ref var school = ref world.Get<School>(enrollment.School);
            school.Enrolled = Math.Max(0, school.Enrolled - 1);
        }

        index?.OnUnenrolled(enrollment.School, enrollment.IsUnenrolled);
        world.Remove<Enrollment>(sim);
        return true;
    }
}
