namespace Genelife.Api.Services;

public enum CommandType
{
    CreateGenos,
    DeleteGenos,
    SetAction,
    CreateCompany,
    DeleteCompany,
    CreateJobPosting
}

public readonly record struct EnqueuedCommand(
    Guid MessageId,
    CommandType Type,
    ulong? NetworkId,
    string? Payload
);