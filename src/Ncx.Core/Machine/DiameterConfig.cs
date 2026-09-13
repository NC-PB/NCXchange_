namespace Ncx.Core.Machine;

/// <summary>
/// [diameter]: the templates that switch diameter programming on and off, when the X axis has programming =
/// "switchable" (machine-config 4, D60).
/// </summary>
public sealed record DiameterConfig
{
    /// <summary>
    /// ON: "DIAMON" on Siemens; null when left out.
    /// </summary>
    public string? On { get; init; }

    /// <summary>
    /// OFF: "DIAMOF" on Siemens; null when left out.
    /// </summary>
    public string? Off { get; init; }
}
