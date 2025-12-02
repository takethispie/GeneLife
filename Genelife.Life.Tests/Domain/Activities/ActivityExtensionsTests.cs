using FluentAssertions;
using Genelife.Life.Domain;
using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Tests.Domain.Activities;

public class ActivityExtensionsTests
{
    [Fact]
    public void ToEnum_ShouldThrowForUnknownActivity()
    {
        // Arrange
        var unknownActivity = new UnknownActivity();

        // Act & Assert
        var action = () => unknownActivity.ToEnum();
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private class UnknownActivity : ILivingActivity
    {
        public int TickDuration { get; set; } = 1;
        public Human Apply(Human being) => being;
    }
}