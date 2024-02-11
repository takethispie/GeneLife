using System.Numerics;

namespace GeneLife.Core.ObjectiveActions
{
    public struct MoveTo : IObjective
    {
        public int Priority { get; set; }
        public Vector3 Target { get; init; }
        public string Name { get; init; }

        public MoveTo(int priority, Vector3 target)
        {
            Priority = priority;
            Target = target;
            Name = GetName();
        }

        public static string GetName() => "MoveTo";
    }
}