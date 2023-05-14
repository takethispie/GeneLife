using Arch.Core.Utils;
using GeneLife.Entities.Exceptions;
using GeneLife.Entities.Interfaces;

namespace GeneLife.Entities;

public class ArchetypeFactory
{
    private List<IArchetypeBuilder> factories;

    public ArchetypeFactory()
    {
        factories = new List<IArchetypeBuilder>();
    }

    public void RegisterFactory(IArchetypeBuilder factory)
    {
        // an archetype is already registered with one of the already added factories 
        if (factory.ArchetypesList().Any(IsBuildableArchetype)) throw new AlreadyRegisteredArchetypeException();
        factories.Add(factory);
    }

    public bool IsBuildableArchetype(string name) => 
        factories.Any(x => x.ArchetypesList().Any(archetype => archetype == name));

    public ComponentType[] Build(string name)
    {
        var factory = factories.FirstOrDefault(x => x.ArchetypesList().Any(arch => arch == name));
        return factory?.Build(name);
    }
}