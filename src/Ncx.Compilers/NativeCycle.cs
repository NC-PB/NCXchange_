using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// A cycle written natively, CYCLE:HEIDENHAIN=251 Q215=0 Q218=60, which compiles only to that controller family
/// (language 4.7.1, D94).
/// </summary>
public sealed record NativeCycle
{
    /// <summary>
    /// The controller family the block names, HEIDENHAIN.
    /// </summary>
    public required string Controller { get; init; }

    /// <summary>
    /// The native cycle as the block writes it, 251 (language 4.7; its form for Fanuc and Siemens is D144).
    /// </summary>
    public required string Number { get; init; }

    /// <summary>
    /// The native parameters in source order: every word of the block that the word catalog does not know (language
    /// 4.7.1, D94).
    /// </summary>
    public required IReadOnlyList<Word> Parameters { get; init; }
}
