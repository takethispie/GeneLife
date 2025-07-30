using Genelife.Domain.Interfaces;
using Genelife.Main.Domain.Activities;

namespace Genelife.Main.Domain;

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
            Work => ActivityEnum.Work,
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null)
        };
    }
}