using Genelife.Domain;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Events.Jobs;
using MassTransit;
using Serilog;

namespace Genelife.Main.Consumers;

public class JobApplicationConsumer : IConsumer<CreateJobPosting>
{
    private readonly Random random = new();
    
    public async Task Consume(ConsumeContext<CreateJobPosting> context)
    {
        var jobPosting = context.Message;
        
        // Simulate some delay before applications start coming in
        await Task.Delay(TimeSpan.FromSeconds(random.Next(1, 5)));
        
        // Generate 3-8 applications for each job posting
        var numApplications = random.Next(3, 9);
        
        for (int i = 0; i < numApplications; i++)
        {
            // Create a simulated human applicant
            var humanId = Guid.NewGuid();
            var skills = GenerateRandomSkills(jobPosting.Requirements);
            var experience = random.Next(0, 15); // 0-15 years experience
            var requestedSalary = GenerateRequestedSalary(jobPosting.SalaryMin, jobPosting.SalaryMax);
            
            // Submit application with some delay between applications
            await Task.Delay(TimeSpan.FromSeconds(random.Next(1, 10)));
            
            await context.Publish(new SubmitJobApplication(
                jobPosting.CompanyId, // JobPostingId - using CompanyId as placeholder
                humanId,
                requestedSalary,
                GenerateCoverLetter(jobPosting.Title),
                skills,
                experience
            ));
            
            Log.Information($"Simulated application submitted for {jobPosting.Title} by {humanId}");
        }
    }
    
    private List<string> GenerateRandomSkills(List<string> requiredSkills)
    {
        var skills = new List<string>();
        
        // Add some required skills (70% chance for each)
        foreach (var skill in requiredSkills)
        {
            if (random.NextDouble() < 0.7)
            {
                skills.Add(skill);
            }
        }
        
        // Add some additional random skills
        var additionalSkills = new List<string>
        {
            "Communication", "Problem Solving", "Team Work", "Leadership",
            "Time Management", "Critical Thinking", "Adaptability", "Creativity",
            "Customer Service", "Project Management", "Data Analysis", "Research"
        };
        
        var numAdditional = random.Next(2, 6);
        var selectedAdditional = additionalSkills
            .OrderBy(x => random.Next())
            .Take(numAdditional)
            .ToList();
            
        skills.AddRange(selectedAdditional);
        
        return skills.Distinct().ToList();
    }
    
    private decimal GenerateRequestedSalary(decimal salaryMin, decimal salaryMax)
    {
        // Generate salary request within range, with some variation
        var range = salaryMax - salaryMin;
        var variation = (decimal)(random.NextDouble() * 0.3 + 0.85); // 0.85 to 1.15 multiplier
        var baseSalary = salaryMin + (range * (decimal)random.NextDouble());
        
        return Math.Round(baseSalary * variation, 0);
    }
    
    private string GenerateCoverLetter(string jobTitle)
    {
        var templates = new[]
        {
            $"I am very interested in the {jobTitle} position and believe my skills and experience make me a strong candidate.",
            $"I would like to apply for the {jobTitle} role. I have relevant experience and am excited about this opportunity.",
            $"Please consider my application for the {jobTitle} position. I am passionate about this field and eager to contribute.",
            $"I am writing to express my interest in the {jobTitle} role. My background aligns well with your requirements.",
            $"I am excited to apply for the {jobTitle} position and would welcome the opportunity to discuss my qualifications."
        };
        
        return templates[random.Next(templates.Length)];
    }
}