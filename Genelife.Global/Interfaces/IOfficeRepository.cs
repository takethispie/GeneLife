using System.Numerics;

namespace Genelife.Global.Interfaces;

public interface IOfficeRepository
{
    Task<Vector3> GetOfficePosition(Guid officeId);
}