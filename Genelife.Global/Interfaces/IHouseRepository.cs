using System.Numerics;

namespace Genelife.Global.Interfaces;

public interface IHouseRepository
{
    Task<Vector3> GetHousePosition(Guid houseId);
}