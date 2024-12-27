namespace Genelife.Domain.Human;

public class Need(NeedType type, float decayRate) {
    public NeedType Type { get; } = type;
    public float Value { get; private set; } = 100f;
    private float DecayRate { get; init; } = decayRate;

    public void Modify(float amount)
    {
        Value = Math.Clamp(Value + amount, 0f, 100f);
    }

    public void Decay(float minutes)
    {
        Modify(-DecayRate * minutes);
    }
}