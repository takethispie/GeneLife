namespace Genelife.Api.Services;

/// <summary>
/// Informs the simulation whether an event consumer is attached, so it can skip
/// publishing work when nothing will read it. Implemented by infrastructure.
/// </summary>
public interface IEventConsumerProbe
{
    bool HasConsumer { get; }
}
