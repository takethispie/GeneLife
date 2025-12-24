using System.Numerics;

namespace Genelife.API.DTOs;

public record CreateOfficeRequest(
    Vector3 Location,
    string Name,
    Guid OwningCompanyId
);