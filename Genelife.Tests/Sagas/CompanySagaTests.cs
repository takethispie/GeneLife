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

        // Act
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));

        // Assert
        (await harness.Consumed.Any<CompanyCreated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<CompanyCreated>()).Should().BeTrue();
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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
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
        var salary = 75000m;

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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new EmployeeHired(company.Id, humanId, salary));

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
        var productivityScore = 0.85m;

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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new EmployeeProductivityUpdated(company.Id, humanId, productivityScore));

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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new StartHiring(company.Id, positionsNeeded));

        // Assert
        (await harness.Consumed.Any<StartHiring>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<StartHiring>()).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPayroll_ShouldBeConsumed()
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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new ProcessPayroll(company.Id));

        // Assert
        (await harness.Consumed.Any<ProcessPayroll>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<ProcessPayroll>()).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateWorkProgress_ShouldBeConsumed()
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

        // Create the saga first
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new UpdateWorkProgress(company.Id));

        // Assert
        (await harness.Consumed.Any<UpdateWorkProgress>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<UpdateWorkProgress>()).Should().BeTrue();
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

        // Act
        await harness.Bus.Publish(new CompanyCreated(company.Id, company));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeHired(company.Id, humanId, 75000m));
        await Task.Delay(100);

        await harness.Bus.Publish(new EmployeeProductivityUpdated(company.Id, humanId, 0.9m));
        await Task.Delay(100);

        await harness.Bus.Publish(new DayElapsed());

        // Assert
        (await harness.Consumed.Any<CompanyCreated>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await harness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await harness.Consumed.Any<DayElapsed>()).Should().BeTrue();

        (await sagaHarness.Consumed.Any<CompanyCreated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeHired>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<EmployeeProductivityUpdated>()).Should().BeTrue();
        (await sagaHarness.Consumed.Any<DayElapsed>()).Should().BeTrue();
    }
}