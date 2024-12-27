namespace Genelife.Domain.Human;

public class Activity(
    string name,
    int duration,
    Dictionary<NeedType, float> needEffects,
    Dictionary<Mood, float> moodEffects) {
    public string Name { get; } = name;
    public int Duration { get; set; } = duration;
    public Dictionary<NeedType, float> NeedEffects { get; } = needEffects;
    public Dictionary<Mood, float> MoodEffects { get; } = moodEffects;
}