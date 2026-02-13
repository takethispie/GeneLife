using System.Numerics;

namespace Genelife.API.DTOs;

public record CreateHouseRequest(
    Guid HumanId,
    Vector3 Location,
    List<Guid>? AdditionalOwners = null
);