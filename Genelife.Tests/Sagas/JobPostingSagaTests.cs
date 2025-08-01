using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Work;
using Genelife.Main.Domain;
using Genelife.Main.Sagas;
using Genelife.Main.Usecases;
using Genelife.Tests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.Tests.Sagas;

public class JobPostingSagaTests
{
    [Fact]
    public async Task JobPostingCreated_ShouldInitializeSagaState()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculateMatchScore>()
            .AddSingleton<CalculateOfferSalary>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<JobPostingSaga, JobPostingSagaState>();

        // Act
        await harness.Bus.Publish(new CreateJobPosting(Guid.NewGuid(), jobPosting.CompanyId, jobPosting));

        // Assert
        (await harness.Consumed.Any<CreateJobPosting>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CreateJobPosting>()).Should().BeTrue();
        (await sagaHarness.Created.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task JobApplicationSubmitted_ShouldBeConsumed()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var jobApplication = TestDataBuilder.CreateJobApplication();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculateMatchScore>()
            .AddSingleton<CalculateOfferSalary>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<JobPostingSaga, JobPostingSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateJobPosting(Guid.NewGuid(), jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new JobApplicationSubmitted(jobApplication.JobPostingId, jobApplication.HumanId, jobApplication));

        // Assert
        (await harness.Consumed.Any<JobApplicationSubmitted>()).Should().BeTrue();
    }

    [Fact]
    public async Task ReviewApplication_ShouldBeConsumed()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var applicationId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculateMatchScore>()
            .AddSingleton<CalculateOfferSalary>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<JobPostingSaga, JobPostingSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateJobPosting(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new ReviewApplication(jobPostingId, applicationId, ApplicationStatus.UnderReview));

        // Assert
        (await harness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
    }

    [Fact]
    public async Task DayElapsed_ShouldBeConsumed()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var jobPostingId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculateMatchScore>()
            .AddSingleton<CalculateOfferSalary>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<JobPostingSaga, JobPostingSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateJobPosting(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleEvents_ShouldBeHandledInSequence()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var jobApplication = TestDataBuilder.CreateJobApplication();
        var humanId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        var jobApplicationId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculateMatchScore>()
            .AddSingleton<CalculateOfferSalary>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<JobPostingSaga, JobPostingSagaState>();

        // Act
        await harness.Bus.Publish(new CreateJobPosting(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100);

        await harness.Bus.Publish(new JobApplicationSubmitted(jobApplicationId, jobApplication.HumanId, jobApplication));
        await Task.Delay(100);

        await harness.Bus.Publish(new ReviewApplication(jobPostingId, jobApplicationId, ApplicationStatus.UnderReview));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<CreateJobPosting>()).Should().BeTrue();
        (await harness.Consumed.Any<JobApplicationSubmitted>()).Should().BeTrue();
        (await harness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();

        (await sagaHarness.Consumed.Any<CreateJobPosting>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}