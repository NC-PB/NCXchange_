namespace Ncx.Cli;

// The codes of the machine a run is checked against, CLI200-CLI249 (P2-04): the machine of --machine by name, ncx.toml
// of the working directory and the cycle catalog of the machine.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI200: --machine, or the machine key of ncx.toml, names no file at that path and no machine file of that name
    /// in the machine folders; decided before the run starts, exit code 2 (D97; architecture 10, machine-config 10).
    /// </summary>
    public const string MachineNotFound = "CLI200";

    /// <summary>
    /// CLI201: ncx.toml of the working directory cannot be read as UTF-8 text; decided before the run starts, exit
    /// code 2 (D97; architecture 10).
    /// </summary>
    public const string ProjectFileUnreadable = "CLI201";

    /// <summary>
    /// CLI202: the cycle catalog that [cycles] catalog of the machine names is in none of the cycle folders, or cannot
    /// be read as UTF-8 text; decided before the run starts, exit code 2 (D97; machine-config 6, 10).
    /// </summary>
    public const string CycleCatalogUnreadable = "CLI202";
}
