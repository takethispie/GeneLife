using Arch.Core.Utils;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;

namespace GeneLife.Core.Entities.Factories;

public class ArchetypeFactory
{
    private readonly List<IArchetypeBuilder> factories;

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

    private bool IsBuildableArchetype(string name) => 
        factories.Any(x => x.ArchetypesList().Any(archetype => archetype == name));

    public ComponentType[] Build(string name)
    {
        var factory = factories.FirstOrDefault(x => x.ArchetypesList().Any(arch => arch.ToLower() == name.ToLower()));
        if (factory == null) throw new ArchetypeNotFoundException();
        return factory.Build(name);
    }
}