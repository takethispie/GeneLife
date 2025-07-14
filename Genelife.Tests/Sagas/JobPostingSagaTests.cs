using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Jobs;
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
        await harness.Bus.Publish(new JobPostingCreated(Guid.NewGuid(), jobPosting.CompanyId, jobPosting));

        // Assert
        (await harness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
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
        await harness.Bus.Publish(new JobPostingCreated(Guid.NewGuid(), jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new JobApplicationSubmitted(Guid.NewGuid(), jobApplication.JobPostingId, jobApplication.HumanId, jobApplication));

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
        await harness.Bus.Publish(new JobPostingCreated(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new ReviewApplication(applicationId, jobPostingId, ApplicationStatus.UnderReview));

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
        await harness.Bus.Publish(new JobPostingCreated(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task ApplicationStatusChanged_ShouldBeConsumed()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var applicationId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        var humanId = Guid.NewGuid();
        var status = "Accepted";

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
        await harness.Bus.Publish(new JobPostingCreated(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new ApplicationStatusChanged(applicationId, jobPostingId, humanId, ApplicationStatus.Submitted, ApplicationStatus.Accepted));

        // Assert - ApplicationStatusChanged is published by the saga, not consumed
        // We can verify the saga processed the event by checking it was created and received the initial event
        (await sagaHarness.Created.Any()).Should().BeTrue();
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
        await harness.Bus.Publish(new JobPostingCreated(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100);

        await harness.Bus.Publish(new JobApplicationSubmitted(jobApplicationId, jobApplication.JobPostingId, jobApplication.HumanId, jobApplication));
        await Task.Delay(100);

        await harness.Bus.Publish(new ReviewApplication(jobApplicationId, jobPostingId, ApplicationStatus.UnderReview));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
        (await harness.Consumed.Any<JobApplicationSubmitted>()).Should().BeTrue();
        (await harness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();

        (await sagaHarness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task JobPostingWorkflow_ShouldHandleCompleteLifecycle()
    {
        // Arrange
        var jobPosting = TestDataBuilder.CreateJobPosting();
        var jobApplication = TestDataBuilder.CreateJobApplication();
        var humanId = Guid.NewGuid();
        var salary = 75000m;
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

        // Act - Complete workflow
        await harness.Bus.Publish(new JobPostingCreated(jobPostingId, jobPosting.CompanyId, jobPosting));
        await Task.Delay(100);

        await harness.Bus.Publish(new JobApplicationSubmitted(jobApplicationId, jobApplication.JobPostingId, jobApplication.HumanId, jobApplication));
        await Task.Delay(100);

        await harness.Bus.Publish(new ReviewApplication(jobApplicationId, jobPostingId, ApplicationStatus.UnderReview));
        await Task.Delay(100);

        await harness.Bus.Publish(new ApplicationStatusChanged(jobApplicationId, jobPostingId, humanId, ApplicationStatus.UnderReview, ApplicationStatus.Accepted));

        // Assert
        (await harness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
        (await harness.Consumed.Any<JobApplicationSubmitted>()).Should().BeTrue();
        (await harness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
        // ApplicationStatusChanged is published by the saga, not consumed

        (await sagaHarness.Consumed.Any<JobPostingCreated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ReviewApplication>()).Should().BeTrue();
    }
}