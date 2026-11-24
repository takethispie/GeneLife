namespace Genelife.Social;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    public int MaxLength { get; set; } = 240;
    public int SayRadius { get; set; } = 12;
    public int RetentionDays { get; set; } = 30;

    public int SayPerWindow { get; set; } = 6;
    public int VenuePerWindow { get; set; } = 6;
    public int WhisperPerWindow { get; set; } = 3;
    public int GlobalSayPerWindow { get; set; } = 60;
    public int GlobalVenuePerWindow { get; set; } = 40;
    public int GlobalWhisperPerWindow { get; set; } = 20;
    public int RateWindowSeconds { get; set; } = 10;

    public int EmoteCooldownSeconds { get; set; } = 3;
    public int EmoteVenueBudget { get; set; } = 20;
    public int EmoteVenueWindowSeconds { get; set; } = 10;

    public int RetentionSweepMinutes { get; set; } = 60;

    public void Validate()
    {
        if (MaxLength < 1)
            throw new InvalidOperationException("Chat:MaxLength must be positive.");
        if (SayRadius < 1)
            throw new InvalidOperationException("Chat:SayRadius must be positive.");
        if (RetentionDays < 1)
            throw new InvalidOperationException("Chat:RetentionDays must be positive.");
        if (RateWindowSeconds < 1)
            throw new InvalidOperationException("Chat:RateWindowSeconds must be positive.");
        if (SayPerWindow < 1 || VenuePerWindow < 1 || WhisperPerWindow < 1)
            throw new InvalidOperationException("Chat rate limits must be positive.");
        if (GlobalSayPerWindow < 1 || GlobalVenuePerWindow < 1 || GlobalWhisperPerWindow < 1)
            throw new InvalidOperationException("Chat global rate limits must be positive.");
        if (EmoteCooldownSeconds < 0 || EmoteVenueBudget < 1 || EmoteVenueWindowSeconds < 1)
            throw new InvalidOperationException("Chat emote limits must be valid.");
        if (RetentionSweepMinutes < 1)
            throw new InvalidOperationException("Chat:RetentionSweepMinutes must be positive.");
    }
}
