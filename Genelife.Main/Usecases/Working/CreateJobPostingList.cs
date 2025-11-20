using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Serilog;

namespace Genelife.Main.Usecases.Working;

public class CreateJobPostingList {
    public List<CreateJobPosting> Execute(Company company, int? publishedJobPostings, Guid correlationId) {
        var postings = new List<CreateJobPosting>();
        var positionsNeeded = new EvaluateHiring().Execute(company);
        if (positionsNeeded == 0 || publishedJobPostings is not null)
            return postings;
        for (var i = 0; i < positionsNeeded; i++) {
            var jobLevel = GetNeededRankAccordingToCompanySize(company.EmployeeIds.Count);
                            
            var jobPosting = new GenerateJobPosting().GenerateForCompany(
                correlationId, 
                company.Type, 
                jobLevel, 
                1
            );
            
            var id = Guid.NewGuid();
            postings.Add(new CreateJobPosting(id, jobPosting));
            Log.Information($"Company {company.Name}: Created job posting for {jobPosting.Title} with salary range {jobPosting.SalaryMin:C} - {jobPosting.SalaryMax:C}");
        }
        return postings;
    }
    
    private static JobLevel GetNeededRankAccordingToCompanySize(int size) => size switch
    {
        < 5 => JobLevel.Entry,
        < 15 => JobLevel.Junior,
        < 30 => JobLevel.Mid,
        < 50 => JobLevel.Senior,
        _ => JobLevel.Lead
    };
}