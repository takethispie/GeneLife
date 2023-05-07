using Arch.Core;

namespace GeneLife;

public class GeneLife
{
    public World Main { get; init; }

    public GeneLife()
    {
        Main = World.Create();
    }
}