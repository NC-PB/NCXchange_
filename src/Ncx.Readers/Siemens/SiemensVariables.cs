using System.Text.RegularExpressions;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The variables of a SINUMERIK program (controllers siemens.md 8, 11 rule 9; controller-mapping 6, VAR; language 4.9):
/// R1=10 and R[1]=10 as VAR:R1=10, an assignment to a DEF name or a name the machine declares (GUD, the builder's
/// variables) as VAR:NAME, DEF INT and DEF REAL with their values as VAR:NAME=value; DEF of the other types, an
/// indirect R[R1], a name NCX cannot write stay RAW.
/// </summary>
internal static partial class SiemensVariables
{
    // The addresses with an equals sign that are words of the language, not variables (controllers siemens.md 3 to 6).
    private static readonly HashSet<string> s_addresses = new(StringComparer.Ordinal)
    {
        "CR", "AR", "TURN", "ANG", "CHF", "CHR", "RND", "RNDM", "FRC", "FRCM", "AP", "RP", "RPL", "FP", "FB", "FZ",
        "FL", "FA", "FGREF", "OVR", "ACC", "LIMS", "SPOS", "SPOSA", "ADIS", "ADISPOS", "CTOL", "OTOL", "ATOL", "OFFN",
        "DISC", "SVC", "SCC", "LEAD", "TILT", "PL", "ALF", "EXTCALL", "DITS", "DITE", "ID", "IDS",
    };

    // The numeric types of DEF (controllers siemens.md 8; controller-mapping 6, VAR; siemens 11 rule 8).
    private static readonly HashSet<string> s_numericTypes = new(StringComparer.Ordinal) { "INT", "REAL", "BOOL" };

    /// <summary>
    /// Tells whether an address with an equals sign is a variable: R1, R[1], or a name that is no word of the
    /// language.
    /// </summary>
    /// <param name="address">The address in capitals.</param>
    public static bool IsVariable(string address)
    {
        if (RParameter().IsMatch(address))
        {
            return true;
        }

        return address.Length > 1 && !address.StartsWith('$') && !address.Contains('[')
            && !LetterWithNumber().IsMatch(address) && !s_addresses.Contains(address)
            && Name().IsMatch(address);
    }

    // TODO(question): controller-mapping 6 keeps the type of a DEF for the Siemens compiler, and no NCX word carries a
    // type; the reader writes VAR:NAME=value without it, and a DEF that gives a name no value stays RAW, since a VAR
    // needs a value (language 4.9).

    /// <summary>
    /// Reads DEF: a numeric type with a value for every name is VAR:NAME=value; the type stays for the Siemens
    /// compiler (controller-mapping 6, VAR); every other DEF is RAW (siemens 11 rule 8).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="statement">The DEF word with the rest of the block.</param>
    public static void ReadDefinition(SiemensBlock block, SourceWord statement)
    {
        block.MarkAllRead();
        string text = statement.Text;
        int typeEnd = SiemensScanner.ReadIdentifier(text, 0);
        string type = typeEnd < 0 ? "" : text.Substring(0, typeEnd).ToUpperInvariant();
        if (!s_numericTypes.Contains(type) || text.Substring(typeEnd).Trim().Length == 0)
        {
            block.Draft.KeepAsRaw($"DEF {type} declares a variable of a type that is no number, or with a scope, which "
                + "NCX keeps as RAW (controllers siemens.md 11 rule 8; controller-mapping 9)");
            block.Draft.RawKeepsState = true;
            return;
        }

        var assignments = new List<Word>();
        foreach (string declaration in SiemensArguments.Split(string.Concat("(", text.AsSpan(typeEnd), ")")))
        {
            int equals = declaration.IndexOf('=', StringComparison.Ordinal);
            string name = (equals < 0 ? declaration : declaration.Substring(0, equals)).Trim().ToUpperInvariant();
            if (equals < 0 || !Name().IsMatch(name))
            {
                block.Draft.KeepAsRaw($"DEF {type} {name} gives the variable no value, or declares an array, and a "
                    + "VAR needs a value (language 4.9; controller-mapping 6, VAR)");
                block.Draft.RawKeepsState = true;
                return;
            }

            Value? value = SiemensExpression.ValueOf(block, declaration.Substring(equals + 1).Trim(),
                out string? problem);
            if (value is null)
            {
                block.Draft.KeepAsRaw(problem!);
                return;
            }

            assignments.Add(new Word { Key = "VAR", Addr = name, Value = value });
        }

        foreach (Word assignment in assignments)
        {
            block.Draft.AddState(assignment.Key, assignment.Addr, assignment.Value);
        }
    }

    /// <summary>
    /// Reads the assignments of a block to variables, R1=10, R[1]=R2+1, COUNT=COUNT+1, WKZ_NR=1.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void ReadAssignments(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length == 0 || word.Text.StartsWith('(') || !IsVariable(word.Address))
            {
                continue;
            }

            block.MarkRead(word);
            string? name = NameOf(word.Address);
            if (name is null)
            {
                block.Draft.KeepAsRaw($"{word.Address} is an indirect R parameter (controller-mapping 6, VAR)");
                return;
            }

            Value? value = SiemensArguments.StringOf(word.Text) is string text
                ? new StringValue(text)
                : SiemensExpression.ValueOf(block, word.Text, out string? problem);
            if (value is null)
            {
                block.Draft.KeepAsRaw($"{word.Address}={word.Text} has no NCX value (language 4.12)");
                return;
            }

            block.Draft.AddState("VAR", name, value);
        }
    }

    /// <summary>
    /// The NCX name of a variable: R1 of R1 and R[1], the name itself otherwise; null for an indirect R[R1].
    /// </summary>
    /// <param name="address">The address in capitals.</param>
    public static string? NameOf(string address)
    {
        Match parameter = RParameter().Match(address);
        if (!parameter.Success)
        {
            return address;
        }

        if (parameter.Groups[1].Success)
        {
            return "R" + parameter.Groups[1].Value;
        }

        return parameter.Groups[2].Success ? "R" + parameter.Groups[2].Value.TrimStart('0').PadLeft(1, '0') : null;
    }

    // R1, R[1], and the indirect R[R1].
    [GeneratedRegex(@"^R(?:([0-9]+)|\[([0-9]+)\]|\[[^\]]*\])$", RegexOptions.CultureInvariant)]
    private static partial Regex RParameter();

    [GeneratedRegex(@"^[A-Z][0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LetterWithNumber();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Name();
}
