using System.Numerics;
using Genelife.Physical.Domain;

namespace Genelife.Physical.Repository;

public class HumanRepository()
{
    private List<Human> humans = [];

    public void Add(Human human)
    {
        if (humans.Any(x => x.CorrelationId == human.CorrelationId) is not true)
            humans.Add(human);
    }

    public void Remove(Guid guid) => humans = humans.Where(x => x.CorrelationId != guid).ToList();

    public Human Get(Guid guid) => humans.FirstOrDefault(x => x.CorrelationId == guid);

    public Human GetClosest(Vector3 position) => humans.OrderByDescending(x => Vector3.Distance(x.Position, position)).FirstOrDefault();
}