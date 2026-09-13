namespace Ncx.Core.Machine;

/// <summary>
/// [system_variables]: NCX SYS_ names to the native system variables of the control, SYS_POS_X = "#5041", with
/// {index} for the NCX index (machine-config 7, D51).
/// </summary>
public sealed record SystemVariables
{
    /// <summary>
    /// The native template of each SYS_ name, in file order; an empty template names a value the control cannot read.
    /// </summary>
    public IReadOnlyDictionary<string, string> Entries { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The native template of a SYS_ name, "#5041" for SYS_POS_X; null when the file does not map it.
    /// </summary>
    /// <param name="name">The NCX name, SYS_POS_X.</param>
    public string? Find(string name)
    {
        return Entries.TryGetValue(name, out string? template) ? template : null;
    }
}
