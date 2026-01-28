using System.Numerics;

namespace Genelife.Global.Messages.DTOs;

public record Position(Vector3 Location, string? LocationLabel);