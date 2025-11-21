using FluentAssertions;
using Genelife.Global.Consumers;
using Genelife.Global.Messages.Commands.Clock;
using Genelife.Global.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Genelife.Global.Tests.Consumers;

public class ClockConsumerTests
{
    [Fact]
    public async Task StartClock_ShouldConsumeMessage()
    {
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
        await harness.Bus.Publish(new StartClock());
        (await harness.Consumed.Any<StartClock>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<StartClock>()).Should().BeTrue();
    }

    [Fact]
    public async Task StopClock_ShouldConsumeMessage()
    {
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
        await harness.Bus.Publish(new StopClock());
        (await harness.Consumed.Any<StopClock>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<StopClock>()).Should().BeTrue();
    }

    [NUnit.Framework.Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task SetClockSpeed_ShouldConsumeMessage(int milliseconds)
    {
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
        await harness.Bus.Publish(new SetClockSpeed(milliseconds));
        (await harness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
        (await consumerHarness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
    }

    [Fact]
    public async Task ClockConsumer_ShouldHandleMultipleCommands()
    {
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

        await harness.Bus.Publish(new StartClock());
        await harness.Bus.Publish(new SetClockSpeed(500));
        await harness.Bus.Publish(new StopClock());

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

        await harness.Bus.Publish(new SetClockSpeed(1));
        await harness.Bus.Publish(new SetClockSpeed(10000));
        (await consumerHarness.Consumed.Any<SetClockSpeed>()).Should().BeTrue();
    }
}