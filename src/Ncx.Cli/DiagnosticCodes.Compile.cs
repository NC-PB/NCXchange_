namespace Ncx.Cli;

// The codes of ncx compile, CLI400-CLI449 (P3-03): the machine file that compile requires, the compiler that the
// controller of the machine chooses, and the tool table that the machine names (D10).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI400: ncx compile names no machine file, neither by --machine nor by the machine key of ncx.toml; compile
    /// always needs one and never runs against the built-in default machine; decided before the run starts, exit code
    /// 2 (D77, D97, D103; architecture 10).
    /// </summary>
    public const string CompileMachineRequired = "CLI400";

    /// <summary>
    /// CLI401: ncx has no compiler for the controller of the machine file, so the file is not compiled; the inputs
    /// were read, so the exit code is 1 (architecture 8; machine-config 1; D97).
    /// </summary>
    public const string NoCompilerForController = "CLI401";

    /// <summary>
    /// CLI402: the tool table that [machine] tool_table names is there and cannot be read as UTF-8 text; decided
    /// before the run starts, exit code 2 (machine-config 1, D10, D97). A tool table that is not there is no error:
    /// the program gets the warning block of D10.
    /// </summary>
    public const string ToolTableUnreadable = "CLI402";
}
