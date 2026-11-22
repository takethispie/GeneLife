using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Life;

/// <summary>
/// Derived lifecycle counters shared by population inflow, metrics and admin
/// reads. Rebuilt on restore and adjusted only in apply phases.
/// </summary>
public sealed class LifecycleIndex
{
    private readonly int[] stageCounts = new int[4];

    public Dictionary<Entity, int> EnrollmentBySchool { get; } = new();
    public int EnrolledStudents { get; private set; }
    public int UnenrolledStudents { get; private set; }
    public int TotalLiving { get; private set; }

    public int StageCount(LifeStage stage) => stageCounts[(int)stage];

    public void OnStageChanged(LifeStage from, LifeStage to)
    {
        stageCounts[(int)from] = Math.Max(0, stageCounts[(int)from] - 1);
        stageCounts[(int)to]++;
    }

    public void OnBorn(LifeStage stage)
    {
        stageCounts[(int)stage]++;
        TotalLiving++;
    }

    public void OnDied(LifeStage stage)
    {
        stageCounts[(int)stage] = Math.Max(0, stageCounts[(int)stage] - 1);
        TotalLiving = Math.Max(0, TotalLiving - 1);
    }

    public void OnEnrolled(Entity school, bool unenrolled)
    {
        if (unenrolled)
        {
            UnenrolledStudents++;
            return;
        }

        EnrolledStudents++;
        EnrollmentBySchool.TryGetValue(school, out var count);
        EnrollmentBySchool[school] = count + 1;
    }

    public void OnUnenrolled(Entity school, bool unenrolled)
    {
        if (unenrolled)
        {
            UnenrolledStudents = Math.Max(0, UnenrolledStudents - 1);
            return;
        }

        EnrolledStudents = Math.Max(0, EnrolledStudents - 1);
        if (EnrollmentBySchool.TryGetValue(school, out var count))
        {
            if (count <= 1)
                EnrollmentBySchool.Remove(school);
            else
                EnrollmentBySchool[school] = count - 1;
        }
    }

    public void Rebuild(World world)
    {
        Array.Clear(stageCounts);
        EnrollmentBySchool.Clear();
        EnrolledStudents = 0;
        UnenrolledStudents = 0;
        TotalLiving = 0;

        var living = new QueryDescription().WithAll<Lifecycle>().WithNone<Components.Gen.Dead>();
        world.Query(in living, (ref Lifecycle lifecycle) =>
        {
            stageCounts[(int)lifecycle.Stage]++;
            TotalLiving++;
        });

        var enrolled = new QueryDescription().WithAll<Enrollment>().WithNone<Components.Gen.Dead>();
        world.Query(in enrolled, (ref Enrollment enrollment) =>
        {
            if (enrollment.IsUnenrolled)
            {
                UnenrolledStudents++;
                return;
            }

            EnrolledStudents++;
            EnrollmentBySchool.TryGetValue(enrollment.School, out var count);
            EnrollmentBySchool[enrollment.School] = count + 1;
        });
    }

    public void Clear()
    {
        Array.Clear(stageCounts);
        EnrollmentBySchool.Clear();
        EnrolledStudents = 0;
        UnenrolledStudents = 0;
        TotalLiving = 0;
    }
}
