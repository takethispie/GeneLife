namespace Genelife.Domain.Events.Jobs;

public record JobApplicationSubmitted(Guid ApplicationId, Guid JobPostingId, Guid HumanId, JobApplication Application);