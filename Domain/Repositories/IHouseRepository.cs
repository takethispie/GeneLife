using System.Numerics;
using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Repositories;

public interface IHouseRepository {
    bool IsInhabitant(Guid humanId, Address address);
    Guid CreateHouse(House house);
    bool DeleteHouse(Guid houseId);
    bool UpdateHouse(House house);
    IEnumerable<House> GetHouses();
    House GetHouse(Guid houseId);
    IEnumerable<House> GetHousesNear(Vector3 position, float radius);
}