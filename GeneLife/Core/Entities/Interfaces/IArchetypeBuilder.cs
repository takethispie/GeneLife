using Arch.Core.Utils;

namespace GeneLife.Core.Entities.Interfaces
{
    public interface IArchetypeBuilder
    {
        public ComponentType[] Build(string type);
        public string[] ArchetypesList();
    }
}