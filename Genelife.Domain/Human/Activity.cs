namespace Genelife.Domain.Human;

public class Activity
{
    public string Name { get; }
    public int Duration { get; set; }
    public Dictionary<NeedType, float> NeedEffects { get; }
    public Dictionary<Mood, float> MoodEffects { get; }

    public Activity(string name, int duration, Dictionary<NeedType, float> needEffects, Dictionary<Mood, float> moodEffects)
    {
        Name = name;
        Duration = duration;
        NeedEffects = needEffects;
        MoodEffects = moodEffects;
    }
}