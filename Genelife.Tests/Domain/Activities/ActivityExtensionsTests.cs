using FluentAssertions;
using Genelife.Main.Domain;

namespace Genelife.Tests.Domain.Activities;

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

    private class UnknownActivity : Genelife.Domain.Interfaces.ILivingActivity
    {
        public int TickDuration { get; set; } = 1;
        public Genelife.Domain.Human Apply(Genelife.Domain.Human being) => being;
    }
}