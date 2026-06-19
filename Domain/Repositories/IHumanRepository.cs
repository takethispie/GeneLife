using System.Numerics;
using Domain.Entities;

namespace Domain.Repositories;

public interface IHumanRepository {
    Human GetHuman(Guid humanId);
    IEnumerable<Human> GetHumans();
    IEnumerable<Human> GetHumansNear(Vector3 position, float radius);
    Guid  CreateHuman(Human human);
    bool DeleteHuman(Guid humanId);
    bool UpdateHuman(Human human);
}