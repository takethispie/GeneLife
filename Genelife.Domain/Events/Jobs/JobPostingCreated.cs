namespace Genelife.Domain.Events.Jobs;

public record JobPostingCreated(Guid JobPostingId, Guid CompanyId, JobPosting JobPosting);