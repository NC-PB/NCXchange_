namespace Ncx.Core.Model;

// The codes of the word catalog, PAR150-PAR199 (P0-03): a word whose address or value its catalog entry does not
// accept (language 3, 4; D98).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// PAR150: the word takes no address and is written with one, LINE:X (language 3, ADDR; language 4).
    /// </summary>
    public const string WordTakesNoAddress = "PAR150";

    /// <summary>
    /// PAR151: the word is written with an address only and has none, CENTER=5, VAR=1 (language 4).
    /// </summary>
    public const string WordNeedsAddress = "PAR151";

    /// <summary>
    /// PAR152: the address is not one of the fixed set of the word, OFFSET:LENGTH (language 4).
    /// </summary>
    public const string WordAddressNotAccepted = "PAR152";

    /// <summary>
    /// PAR153: the value is missing or of a value type or identifier the word does not accept, SPINDLE=UP,
    /// CYLINDER=ON (language 3, value types; language 4; D96).
    /// </summary>
    public const string WordValueNotAccepted = "PAR153";

    /// <summary>
    /// PAR154: SKIP=n names a block skip switch outside 1 to 9 (language 4.1).
    /// </summary>
    public const string SkipSwitchOutOfRange = "PAR154";
}
