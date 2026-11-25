namespace Genelife.Api.Services;

/// <summary>
/// Internal record of an intent that passed hub admission but was rejected when
/// the simulation applied it.
/// </summary>
public sealed record IntentRejection(
    string PlayerId,
    long Sequence,
    string Code,
    string Message);
