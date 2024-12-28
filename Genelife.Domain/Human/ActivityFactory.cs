namespace Genelife.Domain.Human;

public static class ActivityFactory {
    public static List<Activity> InitializeBasicHumanActivities() => [
        new("Eat", 30,
            new() { { NeedType.Hunger, 50f } },
            new() { { Mood.Happy, 0.3f }, { Mood.Energetic, 0.2f } }
        ),

        new("Sleep", 480,
            new() { { NeedType.Energy, 100f } },
            new() { { Mood.Energetic, 0.8f } }
        ),

        new("Shower", 20,
            new() { { NeedType.Hygiene, 70f } },
            new() { { Mood.Happy, 0.4f } }
        ),

        new("Watch TV", 60,
            new() {
                { NeedType.Fun, 30f },
                { NeedType.Energy, -10f }
            },
            new() {
                { Mood.Happy, 0.3f },
                { Mood.Tired, 0.2f }
            }
        )
    ];
}