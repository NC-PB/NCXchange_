namespace Ncx.Config;

// The codes of the loaders of the machine file, the job manifest and the vars file, CFG001-CFG099 (P2-01).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// ERROR: the file is not valid TOML, a key written twice in a table included; nothing of it is loaded.
    /// </summary>
    public const string TomlSyntax = "CFG001";

    /// <summary>
    /// WARNING: a key or table the schema does not know, reported on its line with the nearest known one; a WARNING
    /// and not an ERROR, because the schema is a sketch until the compiler exists (P2-01).
    /// </summary>
    public const string UnknownKey = "CFG002";

    /// <summary>
    /// ERROR: a value of the wrong type, a string where a table belongs, reported on its line (P2-01).
    /// </summary>
    public const string WrongType = "CFG003";

    /// <summary>
    /// ERROR: a table without a key it requires, reported on the line of the table that it names (P2-01).
    /// </summary>
    public const string MissingKey = "CFG004";

    /// <summary>
    /// ERROR: a word outside the values its key takes, controller = "fanox" (machine-config 1 to 9).
    /// </summary>
    public const string ValueNotAllowed = "CFG005";

    /// <summary>
    /// ERROR: more than one resource of a kind without an explicit default, two spindles without default_spindle
    /// (virtual machine 3.8 rule 2).
    /// </summary>
    public const string SeveralWithoutDefault = "CFG006";

    /// <summary>
    /// ERROR: a resource names an axis that no [[axis]] declares (machine-config 4, F23).
    /// </summary>
    public const string UndeclaredAxis = "CFG007";
}
