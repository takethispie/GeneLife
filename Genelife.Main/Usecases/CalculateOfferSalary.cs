using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Main.Usecases;

public class CalculateOfferSalary
{
    public decimal Execute(JobPosting jobPosting, JobApplication application)
    {
        var requestedSalary = application.RequestedSalary;
        var salaryMin = jobPosting.SalaryMin;
        var salaryMax = jobPosting.SalaryMax;

        // If requested salary is within range, offer it
        if (requestedSalary >= salaryMin && requestedSalary <= salaryMax)
            return requestedSalary;

        // If requested is below minimum, offer minimum
        if (requestedSalary < salaryMin)
            return salaryMin;

        // If requested is above maximum, negotiate based on match score
        var maxOffer = application.MatchScore >= 0.8m ? salaryMax * 1.1m : salaryMax;
        return Math.Min(requestedSalary, maxOffer);
    }
}