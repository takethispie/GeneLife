using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Events.Clock;
using Genelife.Domain.Events.Company;
using Genelife.Domain.Events.Jobs;
using Genelife.Domain.Commands.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Main.Domain;
using Genelife.Main.Sagas;
using Genelife.Main.Usecases;
using Genelife.Tests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using CreateCompany = Genelife.Domain.Commands.Company.CreateCompany;

namespace Genelife.Tests.Sagas;

public class CompanySagaTests
{
    [Fact]
    public async Task CompanyCreated_ShouldInitializeSagaState()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        // Act
        await harness.Bus.Publish(new CreateCompany(id, company));

        // Assert
        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Created.Any()).Should().BeTrue();
    }

    [Fact]
    public async Task DayElapsed_ShouldBeConsumed()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        // Create the saga first
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task HireEmployee_ShouldBeConsumed()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();
        var salary = 75000f;

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        // Create the saga first
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new EmployeeHired(id, humanId, salary));

        // Assert
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
    }

    [Fact]
    public async Task EmployeeProductivityUpdated_ShouldBeConsumed()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();
        var productivityScore = 0.85f;

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        // Create the saga first
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new EmployeeProductivityUpdated(id, humanId, productivityScore));

        // Assert
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
    }

    [Fact]
    public async Task StartHiring_ShouldBeConsumed()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();
        var positionsNeeded = 3;

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();
        // Create the saga first
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new StartHiring(id, positionsNeeded));

        // Assert
        (await harness.Consumed.Any<StartHiring>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<StartHiring>()).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleEvents_ShouldBeHandledInSequence()
    {
        // Arrange
        var company = TestDataBuilder.CreateCompany();
        var humanId = Guid.NewGuid();

        await using var provider = new ServiceCollection()
            .AddSingleton<CalculatePayroll>()
            .AddSingleton<EvaluateHiring>()
            .AddSingleton<UpdateProductivity>()
            .AddSingleton<GenerateJobPosting>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<CompanySaga, CompanySagaState>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<CompanySaga, CompanySagaState>();
        var id = Guid.NewGuid();        // Act
        await harness.Bus.Publish(new CreateCompany(id, company));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(id, humanId, 75000f));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeProductivityUpdated(id, humanId, 0.9f));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();

        (await sagaHarness.Consumed.Any<CreateCompany>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}