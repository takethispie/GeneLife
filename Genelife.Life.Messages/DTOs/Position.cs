using System.Numerics;

namespace Genelife.Life.Messages.DTOs;

public record Position(Vector3 Location, string? LocationLabel);