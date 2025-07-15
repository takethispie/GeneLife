using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Living;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Generators;
using Genelife.Main.Domain;
using Genelife.Main.Sagas;
using Genelife.Main.Usecases;
using Genelife.Tests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.Tests.Sagas;

public class HumanSagaTests
{
    [Fact]
    public async Task CreateHuman_ShouldInitializeSagaState()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        var correlationId = NewId.NextGuid();

        // Act
        await harness.Bus.Publish(new CreateHuman(correlationId, human));

        // Assert
        (await harness.Consumed.Any<CreateHuman>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CreateHuman>()).Should().BeTrue();
        (await sagaHarness.Created.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task HourElapsed_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(NewId.NextGuid(), human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new HourElapsed());

        // Assert
        (await harness.Consumed.Any<HourElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task SalaryPaid_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(money: 1000.0f);
        var humanId = NewId.NextGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(humanId, human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new SalaryPaid(humanId, 500.0m, 100.0m));

        // Assert
        (await harness.Consumed.Any<SalaryPaid>()).Should().BeTrue();
    }

    [Fact]
    public async Task JobPostingCreated_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();
        var jobPosting = TestDataBuilder.CreateJobPosting();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(NewId.NextGuid(), human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new CreateJobPosting(Guid.NewGuid(), Guid.Parse(jobPosting.CompanyId), jobPosting));
        await Task.Delay(500); // Wait for potential job application processing

        // Assert
        (await harness.Consumed.Any<CreateJobPosting>()).Should().BeTrue();
    }

    [Fact]
    public async Task HireEmployee_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();
        var humanId = NewId.NextGuid();
        var companyId = Guid.NewGuid();
        var salary = 75000m;

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(humanId, human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new EmployeeHired(companyId, humanId, salary));

        // Assert
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
    }

    [Fact]
    public async Task ApplicationStatusChanged_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();
        var humanId = NewId.NextGuid();
        var applicationId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(humanId, human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new ApplicationStatusChanged(
            applicationId, 
            Guid.NewGuid(), // jobPostingId
            humanId, 
            ApplicationStatus.Submitted, 
            ApplicationStatus.Rejected));

        // Assert
        (await harness.Consumed.Any<ApplicationStatusChanged>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ApplicationStatusChanged>()).Should().BeTrue();
    }

    [Fact]
    public async Task Tick_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        await using var provider = new ServiceCollection()
            .AddSingleton<UpdateNeeds>()
            .AddSingleton<ChooseActivity>()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(NewId.NextGuid(), human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new Tick(12)); // Noon

        // Assert
        (await harness.Consumed.Any<Tick>()).Should().BeTrue();
    }
}