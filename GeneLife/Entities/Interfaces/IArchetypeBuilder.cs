using Arch.Core.Utils;

namespace GeneLife.Entities.Interfaces;

public interface IArchetypeBuilder
{
    public ComponentType[] Build(string type);
    public string[] ArchetypesList();
}