using Arch.Core.Utils;

namespace GeneLife.Common.Entities.Interfaces;

public interface IArchetypeBuilder
{
    public ComponentType[] Build(string type);
    public string[] ArchetypesList();
}