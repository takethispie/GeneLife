using System.Numerics;
using Domain.Entities;

namespace Domain.Repositories;

public interface IHumanRepository {
    Human GetHuman(int id);
    IEnumerable<Human> GetHumans();
    IEnumerable<Human> GetHumansNear(Vector3 position, float radius);
    Guid  CreateHuman(Human human);
    bool DeleteHuman(int id);
    bool UpdateHuman(Human human);
}