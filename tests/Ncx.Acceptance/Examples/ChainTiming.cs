namespace Ncx.Acceptance.Examples;

/// <summary>
/// The tests that measure how long the chain takes over 3D_FRAESEN, "well under a second" (implementation 13, P3-02,
/// P3-05, risks): they run after every other test of the assembly and alone, so that the round trips and the batch of
/// P3-07, which run beside each other, do not slow the chain they measure. Their classes carry
/// [Collection(ChainTiming.Name)], the collection of xUnit that this class defines.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ChainTiming
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public const string Name = "Timing";
}
