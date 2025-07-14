namespace Genelife.Domain.Commands.Company;

public record StartHiring(Guid CompanyId, int PositionsNeeded);