using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Life;

/// <summary>
/// Assigns a stage from the public age. Used when migrating sims that predate
/// the lifecycle and when validating restored state.
/// </summary>
public static class LifecycleClassifier
{
    public static LifeStage Classify(int age, LifeStagePolicy? policy = null) =>
        (policy ?? LifeStagePolicy.Default).StageFor(age);


}
