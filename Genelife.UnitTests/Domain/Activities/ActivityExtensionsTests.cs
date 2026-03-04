using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.UnitTests.Domain.Activities;

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
        public bool GoHomeWhenFinished => false;
        public Human Apply(Human being) => being;
    }
}