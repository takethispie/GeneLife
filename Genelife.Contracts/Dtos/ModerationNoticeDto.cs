namespace Genelife.Api.DTOs;

public sealed record ModerationNoticeDto(
    Guid Id,
    string Kind,
    string Reason,
    long CreatedTick,
    long? EndsTick);
