using Ncx.Core.Machine;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// Which NCX axis a source address moves and whether its value is incremental (controller-mapping 2, X= and IX=): the
/// letters of the [[axis]] table with their incremental letters (machine-config 4, D30), in system A U W H V as the
/// incremental X Z C Y (controllers fanuc.md 3), the standard axes X Y Z A B C and the machine axes U V W (D93); every
/// absolute letter is incremental under G91.
/// </summary>
internal static class FanucAxes
{
    // The standard axis names of language 4.3 and the further letters of a machine axis word of D93.
    private const string StandardAxes = "XYZABC";
    private const string MachineAxisLetters = "UVW";

    // U, W, H and V are the incremental X, Z, C and Y of system A (controllers fanuc.md 3).
    private static readonly Dictionary<string, string> s_systemAIncremental = new(StringComparer.Ordinal)
    {
        ["U"] = "X",
        ["W"] = "Z",
        ["H"] = "C",
        ["V"] = "Y",
    };

    /// <summary>
    /// The axis a word moves; null for a word that names no axis on this machine, H of G43 on a mill.
    /// </summary>
    /// <param name="word">The source word.</param>
    /// <param name="block">The block being read, with its machine and its modal state.</param>
    public static FanucAxisWord? Of(SourceWord word, FanucBlock block)
    {
        string address = word.Address;
        if (address.Length == 0 || !char.IsAsciiLetterUpper(address[0]))
        {
            return null;
        }

        // The [[axis]] table carries the address of every axis and of its incremental form (machine-config 4, D30).
        foreach (AxisDef axis in block.Machine.Axes)
        {
            if ((axis.Letter ?? axis.NcxName) == address)
            {
                return new FanucAxisWord(word, axis.NcxName, block.Incremental);
            }

            if (axis.IncrementalLetter == address)
            {
                return new FanucAxisWord(word, axis.NcxName, true);
            }
        }

        // An extended axis name with its number, C2=, is the axis of that NCX name; H2= its incremental form in system
        // A (D30, controller-mapping 2).
        string letter = address.Substring(0, 1);
        string number = address.Substring(1);
        if (number.Length > 0)
        {
            if (block.System == GcodeSystem.A && s_systemAIncremental.TryGetValue(letter, out string? absolute))
            {
                return new FanucAxisWord(word, absolute + number, true);
            }

            return StandardAxes.Contains(letter, StringComparison.Ordinal)
                || MachineAxisLetters.Contains(letter, StringComparison.Ordinal)
                ? new FanucAxisWord(word, address, block.Incremental)
                : null;
        }

        if (block.System == GcodeSystem.A && s_systemAIncremental.TryGetValue(address, out string? axisOfSystemA))
        {
            return new FanucAxisWord(word, axisOfSystemA, true);
        }

        if (StandardAxes.Contains(address, StringComparison.Ordinal)
            || MachineAxisLetters.Contains(address, StringComparison.Ordinal))
        {
            return new FanucAxisWord(word, address, block.Incremental);
        }

        return null;
    }

    /// <summary>
    /// The unread axis words of a block, in source order.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static List<FanucAxisWord> Unread(FanucBlock block)
    {
        var axes = new List<FanucAxisWord>();
        foreach (SourceWord word in block.Unread())
        {
            if (Of(word, block) is FanucAxisWord axis)
            {
                axes.Add(axis);
            }
        }

        return axes;
    }

    /// <summary>
    /// The unread word of an NCX axis in the block; null when there is none.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axis">The NCX axis name, "Z".</param>
    public static FanucAxisWord? Find(FanucBlock block, string axis)
    {
        foreach (FanucAxisWord word in Unread(block))
        {
            if (word.Axis == axis)
            {
                return word;
            }
        }

        return null;
    }
}
