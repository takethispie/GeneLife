using Genelife.Life.Domain.Activities;
using Genelife.Life.Interfaces;

namespace Genelife.Life.Domain;

public enum ActivityEnum {
    Eat,
    Sleep,
    Shower,
    Work
}

public static class ActivityExtensions {
    public static ActivityEnum ToEnum(this ILivingActivity activity) {
        return activity switch {
            Eat => ActivityEnum.Eat,
            Sleep => ActivityEnum.Sleep,
            Shower => ActivityEnum.Shower,
            Domain.Activities.Work => ActivityEnum.Work,
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null)
        };
    }
}