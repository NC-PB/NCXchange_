namespace Ncx.Cli;

// The codes of ncx convert, CLI250-CLI299 (P3-02): the machine file that convert requires and the reader that the
// controller of the machine chooses.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI250: ncx convert names no machine file, neither by --machine nor by the machine key of ncx.toml; convert
    /// always needs one and never runs against the built-in default machine; decided before the run starts, exit code 2
    /// (D77, D97, D103; architecture 10).
    /// </summary>
    public const string MachineRequired = "CLI250";

    /// <summary>
    /// CLI251: ncx has no reader for the controller of the machine file, so the file is not converted; the inputs were
    /// read, so the exit code is 1 (architecture 7; machine-config 1; D97).
    /// </summary>
    public const string NoReaderForController = "CLI251";
}
