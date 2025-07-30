using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Main.Domain;

public record IdentifiedJobApplication(Guid Id, JobApplication Data);