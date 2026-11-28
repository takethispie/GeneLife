namespace Genelife.Api.Services;

/// <summary>
/// A fan-out tap on the simulation's outbound event channel. The canonical
/// consumer persists the stream to the event store; additional sinks observe the
/// same stream without owning the reader. Sinks must be cheap and must never
/// throw — a failing sink is logged and dropped so it can never stall the tick
/// loop's off-thread writer.
/// </summary>
public interface ISimulationEventSink
{
    void OnEvent(object @event, long tick);
}
