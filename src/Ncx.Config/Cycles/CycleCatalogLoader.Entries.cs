using Ncx.Core.Machine;

namespace Ncx.Config.Cycles;

// Machine-config 6 and 5a: the [[cycle]] entries of a catalog file or of a machine file, each as the file writes it.
public static partial class CycleCatalogLoader
{
    // The keys of a catalog entry (machine-config 6) and the four keys of its expansion rule (machine-config 5a).
    private static readonly string[] s_entryKeys =
    [
        "name", "native", "params", "absolute_from_surface", "modal", "contour", "signature", "fixed", "pre", "post",
        "requires", "restore",
    ];

    // The word that the words of absolute_from_surface are relative to (machine-config 6, language 4.7).
    private const string SurfaceWord = "SURFACE";

    /// <summary>
    /// Reads the [[cycle]] entries of a catalog file or of a machine file in file order, each as the file writes it,
    /// with the keys it wrote (machine-config 6). A name written twice is an ERROR on the second entry.
    /// </summary>
    /// <param name="tables">The [[cycle]] tables of the file.</param>
    /// <param name="controller">The controller family the entries belong to; null when the machine file names none.</param>
    internal static IReadOnlyList<CycleEntry> ReadEntries(IReadOnlyList<ConfigTable> tables, Controller? controller)
    {
        var entries = new List<CycleEntry>();
        var names = new List<string>();
        CycleCatalog family = controller is Controller known ? DrillingFamily.Catalog(known) : new CycleCatalog();
        foreach (ConfigTable table in tables)
        {
            CycleEntry? entry = ReadEntry(table, controller);
            if (entry is null)
            {
                continue;
            }

            // The catalog finds an entry by its name, so one file names each cycle once (machine-config 6).
            if (names.Contains(entry.Name))
            {
                table.Diagnostics.Error(table.Line, DiagnosticCodes.CycleNameTwice,
                    $"{table.Name} names the cycle {entry.Name} a second time in this file; the catalog finds an entry "
                    + "by its name (machine-config 6).");
                continue;
            }

            names.Add(entry.Name);
            CheckAbsoluteFromSurface(table, entry, family.Find(entry.Name));
            entries.Add(entry);
        }

        return entries;
    }

    // One entry: name and native are required, every other key is optional, and pre, post, requires and restore make
    // its expansion rule (machine-config 6, 5a). The entry keeps the keys the file wrote, by which it overrides the
    // entry of the same name before it.
    private static CycleEntry? ReadEntry(ConfigTable table, Controller? controller)
    {
        table.WarnUnknownKeys(s_entryKeys);
        string? name = table.RequiredText("name");
        string? native = ReadNative(table);
        if (name is not null)
        {
            CheckName(table, name);
        }

        IReadOnlyDictionary<string, string> parameters = ReadParams(table);
        IReadOnlyList<string> absoluteFromSurface = table.TextList("absolute_from_surface");
        bool modal = table.Flag("modal");
        IReadOnlyList<string> contour = ReadContour(table);
        IReadOnlyList<string> signature = table.TextList("signature");
        IReadOnlyDictionary<string, decimal> fixedValues =
            table.Table("fixed")?.Numbers() ?? new Dictionary<string, decimal>();
        ExpansionRule? rule = MachineConfigLoader.ReadRule(table);
        CheckFamilyKeys(table, controller, modal, signature);
        if (name is null || native is null)
        {
            return null;
        }

        return new CycleEntry
        {
            Name = name,
            Native = native,
            Params = parameters,
            AbsoluteFromSurface = absoluteFromSurface,
            Modal = modal,
            Contour = contour,
            Signature = signature,
            Fixed = fixedValues,
            Rule = rule,
            WrittenKeys = WrittenKeys(table),
        };
    }

    // native: the G code of a Fanuc cycle, the number of a Heidenhain CYCL DEF, the name of a Siemens cycle; a number
    // is kept as its text, the text a reader compares (machine-config 6). Every entry names it, the entries of
    // machine-config 5a and 6 included.
    private static string? ReadNative(ConfigTable table)
    {
        if (!table.Has("native"))
        {
            table.MissingKey("native");
            return null;
        }

        return table.PlaceholderValue("native");
    }

    // A program names the cycle as the value of CYCLE, so the name is an NCX identifier (language 3, 4.7.1).
    private static void CheckName(ConfigTable table, string name)
    {
        if (!IsNcxWord(name))
        {
            table.Diagnostics.Error(table.LineOf("name"), DiagnosticCodes.CycleWordMalformed,
                $"The cycle name {name} in {table.Name} is not an NCX identifier, a capital followed by capitals, "
                + "digits and underscores; no program can name it (language 3, 4.7.1).");
        }
    }

    // params: every key the NCX word of a parameter, every value the native name that carries it; a program writes
    // the words on the cycle block, so each is an NCX key (machine-config 6, language 3).
    private static IReadOnlyDictionary<string, string> ReadParams(ConfigTable table)
    {
        ConfigTable? parameters = table.Table("params");
        if (parameters is null)
        {
            return new Dictionary<string, string>();
        }

        foreach (string word in parameters.Keys)
        {
            if (!IsNcxWord(word))
            {
                table.Diagnostics.Error(parameters.LineOf(word), DiagnosticCodes.CycleWordMalformed,
                    $"The parameter word {word} in {table.Name} is not an NCX key, a capital followed by capitals, "
                    + "digits and underscores; no cycle block can carry it (language 3, machine-config 6).");
            }
        }

        return parameters.Strings(null);
    }

    // contour: two words are the first and the last block of the contour range, one names the contour subprogram
    // (machine-config 6, D65); more cannot be read.
    private static IReadOnlyList<string> ReadContour(ConfigTable table)
    {
        IReadOnlyList<string> contour = table.TextList("contour");
        if (contour.Count > 2)
        {
            table.Diagnostics.Error(table.LineOf("contour"), DiagnosticCodes.CycleContourWordCount,
                $"contour in {table.Name} lists {contour.Count} words; two are the first and the last block of the "
                + "contour, one names the contour subprogram (machine-config 6).");
        }

        return contour;
    }

    // Heidenhain and Siemens entries leave modal out, their calls are words of their own (CYCL CALL, M99, MCALL);
    // Fanuc entries leave the signature out, their parameters are address words of the cycle block (machine-config 6).
    private static void CheckFamilyKeys(
        ConfigTable table, Controller? controller, bool modal, IReadOnlyList<string> signature)
    {
        if (modal && controller is Controller.Heidenhain or Controller.Siemens)
        {
            table.Diagnostics.Warning(table.LineOf("modal"), DiagnosticCodes.CycleKeyOutsideItsFamily,
                $"modal = true in {table.Name}: {controller} entries leave modal out, their calls are words of their "
                + "own (machine-config 6).");
        }

        if (signature.Count > 0 && controller == Controller.Fanuc)
        {
            table.Diagnostics.Warning(table.LineOf("signature"), DiagnosticCodes.CycleKeyOutsideItsFamily,
                $"signature in {table.Name}: Fanuc entries leave the signature out, their parameters are address "
                + "words of the cycle block (machine-config 6).");
        }
    }

    // A word of absolute_from_surface is converted with the surface, NCX = SURFACE + native, so the entry maps the word
    // and SURFACE (machine-config 6). Checked on what the entry holds over the built-in entry of its name; an entry
    // that writes no params and overrides a catalog entry the file cannot see is left to that catalog.
    private static void CheckAbsoluteFromSurface(ConfigTable table, CycleEntry entry, CycleEntry? builtIn)
    {
        bool writesParams = entry.WrittenKeys.Contains("params");
        bool writesAbsolute = entry.WrittenKeys.Contains("absolute_from_surface");
        if ((!writesParams && !writesAbsolute) || (!writesParams && builtIn is null))
        {
            return;
        }

        CycleEntry held = builtIn is null ? entry : builtIn.OverriddenBy(entry);
        int line = table.LineOf(writesAbsolute ? "absolute_from_surface" : "params");
        foreach (string word in held.AbsoluteFromSurface)
        {
            if (held.NativeOf(word) is null)
            {
                table.Diagnostics.Warning(line, DiagnosticCodes.CycleAbsoluteWordUnmapped,
                    $"absolute_from_surface in {table.Name} names {word}, which params does not map; no native value "
                    + "converts to it (machine-config 6).");
            }
        }

        if (held.AbsoluteFromSurface.Count > 0 && held.NativeOf(SurfaceWord) is null)
        {
            table.Diagnostics.Warning(line, DiagnosticCodes.CycleAbsoluteWithoutSurface,
                $"{table.Name} converts from the surface but params maps no SURFACE; NCX = SURFACE + native "
                + "(machine-config 6).");
        }
    }

    // The keys of machine-config 6 and 5a that the file wrote, in file order.
    private static List<string> WrittenKeys(ConfigTable table)
    {
        var keys = new List<string>();
        foreach (string key in table.Keys)
        {
            if (s_entryKeys.Contains(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    // A key and an identifier are a capital followed by capitals, digits and underscores (language 3).
    private static bool IsNcxWord(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetterUpper(text[0]))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
