using System.Numerics;

namespace GeneLife.Athena.Core.Objectives;

public record MoveTo(int Priority, Vector3 target, string Name = "Move To") : IObjective;