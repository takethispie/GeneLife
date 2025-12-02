using FluentAssertions;
using Genelife.Global.Messages.Events.Clock;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Sagas;
using Genelife.Life.Sagas.States;
using Genelife.Life.Tests.TestData;
using Genelife.Life.Usecases;
using Genelife.Work.Generators;
using Genelife.Work.Messages.Events.Company;
using Genelife.Work.Usecases;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.Life.Tests.Sagas;

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
        await harness.Bus.Publish(new SalaryPaid(humanId, 500.0f, 100.0f));

        // Assert
        (await harness.Consumed.Any<SalaryPaid>()).Should().BeTrue();
    }

    [Fact]
    public async Task HireEmployee_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();
        var humanId = NewId.NextGuid();
        var companyId = Guid.NewGuid();
        var salary = 75000f;

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