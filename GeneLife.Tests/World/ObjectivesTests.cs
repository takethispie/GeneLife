using FluentAssertions;
using GeneLife.Athena.Components;
using GeneLife.Core.ObjectiveActions;

namespace GeneLife.Tests.World
{
    public class ObjectivesTests
    {
        [Fact]
        public void Should_Be_Top_Priority()
        {
            var currentObjectives = new IObjective[] { new Eat(5), new Drink(8) };
            var objectives = new Objectives { CurrentObjectives = currentObjectives };
            objectives.IsHighestPriority(typeof(Eat)).Should().NotBe(true);
            objectives.IsHighestPriority(typeof(Drink)).Should().BeTrue();
        }
    }
}