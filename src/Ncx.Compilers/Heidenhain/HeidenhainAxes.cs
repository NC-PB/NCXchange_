using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The axis words of a block as Klartext writes them (controllers heidenhain.md 2; 8 rule 2; language 4.3, 5 rule 6):
/// the address of the [[axis]] table, I in front for the incremental form, a sign on every coordinate, in the order X
/// Y Z A B C and then the machine axes, the absolute words before the incremental ones.
/// </summary>
internal static class HeidenhainAxes
{
    // The incremental prefix of NCX and of Klartext, IX+30 (language 4.3; controllers heidenhain.md 2).
    private const string Incremental = "I";

    // The standard axes in the canonical order of language 5 rule 6, bucket 3.
    private static readonly string[] s_standardOrder = ["X", "Y", "Z", "A", "B", "C"];

    /// <summary>
    /// The axis words of a block in the canonical order: X Y Z A B C, the machine axes after them by name, every
    /// absolute word before the incremental ones (language 5 rule 6).
    /// </summary>
    public static List<HeidenhainAxisWord> Of(Block block)
    {
        var axes = new List<HeidenhainAxisWord>();
        foreach (Word word in block.Words)
        {
            if (WordCatalog.IsStandardAxis(word.Key))
            {
                bool incremental = word.Key.Length == 2 && word.Key.StartsWith(Incremental, StringComparison.Ordinal);
                axes.Add(new HeidenhainAxisWord(word, incremental ? word.Key.Substring(1) : word.Key, incremental));
            }
            else if (word.Definition is null && WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis))
            {
                axes.Add(new HeidenhainAxisWord(word, machineAxis.AxisName, machineAxis.IsIncremental));
            }
        }

        axes.Sort(Compare);
        return axes;
    }

    /// <summary>
    /// Writes axis words, X+10 IY-5, and marks them as written; null when a value has no Klartext form, which is
    /// reported on the block.
    /// </summary>
    public static string? Write(HeidenhainBlock writing, IReadOnlyList<HeidenhainAxisWord> axes)
    {
        var words = new List<string>();
        foreach (HeidenhainAxisWord axis in axes)
        {
            writing.MarkWritten(axis.Word);
            string letter = LetterOf(writing, axis.Axis);
            string? value = HeidenhainNumbers.SignedValue(writing, letter, axis.Word.Value);
            if (value is null)
            {
                HeidenhainNumbers.ReportValue(writing, axis.Word);
                return null;
            }

            words.Add((axis.Incremental ? Incremental : "") + letter + value);
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// The address Klartext writes for an NCX axis name: the letter of its [[axis]] entry, A, B and C resolved for the
    /// workpiece holder (machine-config 4, D30; virtual machine 3.8 rule 3); the name itself where no entry gives one.
    /// </summary>
    public static string LetterOf(HeidenhainBlock writing, string axis)
    {
        MachineConfig machine = writing.Machine;
        AxisDef? definition = writing.After.Frame.WorkpieceHolder is string holder
            ? machine.ResolveAxis(axis, holder)
            : machine.ResolveAxis(axis);
        return definition?.Letter ?? axis;
    }

    /// <summary>
    /// The tool axis of a working plane (language 4.2, WORKPLANE): Z of XY, Y of ZX, X of YZ.
    /// </summary>
    public static string ToolAxis(Workplane plane)
    {
        return plane switch
        {
            Workplane.ZX => "Y",
            Workplane.YZ => "X",
            _ => "Z",
        };
    }

    /// <summary>
    /// The two axes of a working plane, the first and the second (language 4.2; virtual machine 3.2): X and Y of XY, Z
    /// and X of ZX, Y and Z of YZ.
    /// </summary>
    public static string[] PlaneAxes(Workplane plane)
    {
        return plane switch
        {
            Workplane.ZX => ["Z", "X"],
            Workplane.YZ => ["Y", "Z"],
            _ => ["X", "Y"],
        };
    }

    // Absolute before incremental, then X Y Z A B C, then the machine axes by name (language 5 rule 6, bucket 3).
    private static int Compare(HeidenhainAxisWord first, HeidenhainAxisWord second)
    {
        if (first.Incremental != second.Incremental)
        {
            return first.Incremental ? 1 : -1;
        }

        int firstRank = Rank(first.Axis);
        int secondRank = Rank(second.Axis);
        return firstRank != secondRank
            ? firstRank.CompareTo(secondRank)
            : string.CompareOrdinal(first.Axis, second.Axis);
    }

    private static int Rank(string axis)
    {
        int index = Array.IndexOf(s_standardOrder, axis);
        return index < 0 ? s_standardOrder.Length : index;
    }
}
