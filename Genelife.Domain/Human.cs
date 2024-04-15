namespace Genelife.Domain;

public record Human(Guid CorrelationId, string FirstName, string LastName, int Age, Sex Sex);
