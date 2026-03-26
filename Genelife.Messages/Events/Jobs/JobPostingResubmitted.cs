using Genelife.Domain.Work.Job;

namespace Genelife.Messages.Events.Jobs;

public record JobPostingResubmitted(Guid JobPostingId, JobPosting JobPosting);