namespace Genelife.Domain.Human;

public class Need
{
    public NeedType Type { get; }
    public float Value { get; private set; }
    public float DecayRate { get; }

    public Need(NeedType type, float decayRate)
    {
        Type = type;
        Value = 100f;
        DecayRate = decayRate;
    }

    public void Modify(float amount)
    {
        Value = Math.Clamp(Value + amount, 0f, 100f);
    }

    public void Decay(float minutes)
    {
        Modify(-DecayRate * minutes);
    }
}