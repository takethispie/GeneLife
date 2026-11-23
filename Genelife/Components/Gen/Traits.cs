namespace Genelife.Components.Gen;

// V4 canonical trait enums. Values are persisted as integers, so append only:
// never reorder or remove an existing member.
public enum EyeColor : byte { Brown, Blue, Green, Grey, Hazel }
public enum HairColor : byte { Black, Brown, Blonde, Ginger, Grey }
public enum Handedness : byte { Right, Left, Ambidextrous }
public enum Morphotype : byte { Ectomorph, Mesomorph, Endomorph }
public enum Aptitude : byte { Science, Art, Craft, Social, Physical }
public enum HeightPotential : byte { Short, Average, Tall }
public enum Sex : byte { Female, Male }
public enum Temperament : byte { Peaceful, Neutral, Violent }

/// <summary>
/// Heritable trait set created with a sim and restored from snapshots. Age is
/// never stored here; it remains <see cref="Genelife.Components.Gen.Alive.Age"/> and
/// <see cref="Genelife.Components.Life.Lifecycle.AgeTicks"/>.
/// </summary>
public record struct Traits
{
    public EyeColor EyeColor;
    public HairColor HairColor;
    public Handedness Handedness;
    public Morphotype Morphotype;
    public Aptitude Aptitude;
    public HeightPotential HeightPotential;
    public Sex Sex;
    public Temperament Temperament;
}
