using FluentAssertions;
using Genelife.Application.Sagas;
using Genelife.Application.Sagas.States;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Messages.Commands;
using Genelife.Messages.Events.Clock;
using Genelife.Messages.Events.Company;
using Genelife.UnitTests.TestData;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Sagas;

public class HumanSagaTests
{
    [Fact]
    public async Task CreateHuman_ShouldInitializeSagaState()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>(); })
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
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(NewId.NextGuid(), human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new HourElapsed(new TimeOnly(1, 0)));

        // Assert
        (await harness.Consumed.Any<HourElapsed>()).Should().BeTrue();
    }

    [Fact]
    public async Task Tick_ShouldBeConsumed()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        await using var provider = new ServiceCollection()
            .AddSingleton<GenerateEmployment>()
            .AddSingleton<CalculateMatchScore>()
            .AddMassTransitTestHarness(cfg => { cfg.AddSagaStateMachine<HumanSaga, HumanSagaState>(); })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<HumanSaga, HumanSagaState>();

        // Create the saga first
        await harness.Bus.Publish(new CreateHuman(NewId.NextGuid(), human));
        await Task.Delay(100); // Wait for saga creation

        // Act
        await harness.Bus.Publish(new Tick(DateTime.UtcNow)); // Noon

        // Assert
        (await harness.Consumed.Any<Tick>()).Should().BeTrue();
    }
}