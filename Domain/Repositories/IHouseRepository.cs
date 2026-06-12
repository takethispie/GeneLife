using Domain.ValueObjects;

namespace Domain.Repositories;

public interface IHouseRepository {
    bool IsInhabitant(Guid humanId, Address address);
}