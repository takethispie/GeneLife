using FluentAssertions;
using Genelife.Domain.Commands.Clock;
using Genelife.Main.Consumers;
using Genelife.Main.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Genelife.Tests.Consumers;

public class ClockConsumerTests
{
    [Fact]
    public async Task StartClock_ShouldConsumeMessage()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var consumerHarness = harness.GetConsumerHarness<ClockConsumer>();

        // Act
        await harness.Bus.Publish(new StartClock());

        // Assert
        (await harness.Consumed.Any<StartClock>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<StartClock>()).Should().BeTrue();
    }

    [Fact]
    public async Task StopClock_ShouldConsumeMessage()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var consumerHarness = harness.GetConsumerHarness<ClockConsumer>();

        // Act
        await harness.Bus.Publish(new StopClock());

        // Assert
        (await harness.Consumed.Any<StopClock>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<StopClock>()).Should().BeTrue();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task SetClockSpeed_ShouldConsumeMessage(int milliseconds)
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var consumerHarness = harness.GetConsumerHarness<ClockConsumer>();

        // Act
        await harness.Bus.Publish(new SetClockSpeed(milliseconds));

        // Assert
        (await harness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
    }

    [Fact]
    public async Task ClockConsumer_ShouldHandleMultipleCommands()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var consumerHarness = harness.GetConsumerHarness<ClockConsumer>();

        // Act
        await harness.Bus.Publish(new StartClock());
        await harness.Bus.Publish(new SetClockSpeed(500));
        await harness.Bus.Publish(new StopClock());

        // Assert
        (await harness.Consumed.Any<StartClock>()).Should().BeTrue();
        (await harness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
        (await harness.Consumed.Any<StopClock>()).Should().BeTrue();
        
        (await consumerHarness.Consumed.Any<StartClock>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<StopClock>()).Should().BeTrue();
    }

    [Fact]
    public async Task ClockConsumer_ShouldNotThrowOnValidCommands()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act & Assert - Should not throw
        await harness.Bus.Publish(new StartClock());
        await harness.Bus.Publish(new SetClockSpeed(1000));
        await harness.Bus.Publish(new StopClock());

        (await harness.Consumed.Any<StartClock>()).Should().BeTrue();
        (await harness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
        (await harness.Consumed.Any<StopClock>()).Should().BeTrue();
    }

    [Fact]
    public async Task ClockConsumer_ShouldHandleExtremeClockSpeeds()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        var clockService = new ClockService(mockPublishEndpoint.Object);
        
        await using var provider = new ServiceCollection()
            .AddSingleton(clockService)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ClockConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var consumerHarness = harness.GetConsumerHarness<ClockConsumer>();

        // Act
        await harness.Bus.Publish(new SetClockSpeed(1)); // Very fast
        await harness.Bus.Publish(new SetClockSpeed(10000)); // Very slow

        // Assert
        (await consumerHarness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
    }
}