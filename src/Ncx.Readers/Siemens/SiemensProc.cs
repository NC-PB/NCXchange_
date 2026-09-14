namespace Ncx.Readers.Siemens;

/// <summary>
/// The declaration of a subprogram, PROC NAME(REAL LENGTH=10, INT N, VAR REAL RESULT) SBLOF DISPLOF (controllers
/// siemens.md 8; controller-mapping 1 and 6): its name, its parameters in order, and the flags after them.
/// </summary>
internal sealed record SiemensProc
{
    /// <summary>
    /// The name of the subprogram, in capitals.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The parameters in the order a call gives them.
    /// </summary>
    public IReadOnlyList<SiemensParameter> Parameters { get; init; } = [];

    /// <summary>
    /// The flags after the parameters, SBLOF, DISPLOF, SAVE, as written; empty for none.
    /// </summary>
    public IReadOnlyList<string> Flags { get; init; } = [];

    /// <summary>
    /// True when the declaration cannot be read: the reader keeps it and its calls as RAW.
    /// </summary>
    public bool Unreadable { get; init; }

    /// <summary>
    /// True when SAVE restores the modal G codes of the caller at the return (controllers siemens.md 8).
    /// </summary>
    public bool Saves => Flags.Contains("SAVE");
}

/// <summary>
/// A parameter of a PROC: call-by-value with its type and an optional default, or VAR for call-by-reference
/// (controllers siemens.md 8).
/// </summary>
/// <param name="Name">The name, in capitals.</param>
/// <param name="Type">The type as written, REAL, INT, STRING[32].</param>
/// <param name="ByReference">True for a VAR parameter.</param>
/// <param name="Default">The default as written; null for none.</param>
internal sealed record SiemensParameter(string Name, string Type, bool ByReference, string? Default);
