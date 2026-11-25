using Arch.Core;

namespace Genelife.Components.Life;

/// <summary>
/// Education-stage enrollment. An unenrolled student keeps this component with
/// <see cref="IsUnenrolled"/> set so the admin can see the fallback explicitly.
/// </summary>
public record struct Enrollment
{
    public Entity School;
    public long EnrolledAtTick;
    public float Attendance;
    public int MonthsAttended;
    public bool IsUnenrolled;

    public Enrollment(Entity school, long enrolledAtTick, float attendance = 1f, bool isUnenrolled = false)
    {
        School = school;
        EnrolledAtTick = enrolledAtTick;
        Attendance = Math.Clamp(attendance, 0f, 1f);
        MonthsAttended = 0;
        IsUnenrolled = isUnenrolled;
    }
}
