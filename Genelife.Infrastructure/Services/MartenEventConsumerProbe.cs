using Marten;

namespace Genelife.Api.Services;

/// <summary>
/// Reports whether a Marten event consumer is present. The simulation only
/// publish-outbound events when something will persist them.
/// </summary>
public sealed class MartenEventConsumerProbe : IEventConsumerProbe
{
    private readonly IDocumentStore? documentStore;

    public MartenEventConsumerProbe(IDocumentStore? documentStore = null)
    {
        this.documentStore = documentStore;
    }

    public bool HasConsumer => documentStore != null;
}
