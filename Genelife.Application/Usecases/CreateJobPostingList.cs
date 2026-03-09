using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;
using Genelife.Messages.Commands.Jobs;
using Serilog;

namespace Genelife.Application.Usecases;

public class CreateJobPostingList {
    public List<CreateJobPosting> Execute(Company company, int? publishedJobPostings, Guid correlationId, Position officeLocation) {
        var postings = new List<CreateJobPosting>();
        var positionsNeeded = company.GetHiringPotential();
        if (positionsNeeded == 0 || publishedJobPostings is > 0)
            return postings;
        for (var i = 0; i < positionsNeeded; i++) {
            var jobLevel = GetNeededRankAccordingToCompanySize(company.Employees.Count);
                            
            var jobPosting = new GenerateJobPosting().GenerateForCompany(
                correlationId, 
                company.Type, 
                jobLevel, 
                1,
                officeLocation
            );
            
            var id = Guid.NewGuid();
            postings.Add(new CreateJobPosting(id, jobPosting));
            Log.Information("Company {CompanyName}: Created job posting for {JobPostingTitle} with salary range {JobPostingSalaryMin:C} - {JobPostingSalaryMax:C}", company.Name, jobPosting.Title, jobPosting.SalaryMin, jobPosting.SalaryMax);
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