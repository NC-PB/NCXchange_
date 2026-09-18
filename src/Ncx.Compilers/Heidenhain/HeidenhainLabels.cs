using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The labels of one Klartext program (controllers heidenhain.md 1; language 4.9, 4.13): every LABEL of the program and
/// of the subprograms it calls, and every SUB section it calls, is an LBL of the program, since the LBL sections of the
/// subprograms stand between its M30 and its END PGM and Klartext labels are program-local. NCX keeps a LABEL unique
/// per program or subprogram and the SUB names apart from the labels (language 4.9), so two of them may be one LBL of
/// the file; and LBL 0 ends a subprogram (controllers heidenhain.md 1).
/// </summary>
internal static class HeidenhainLabels
{
    // LBL 0 closes the LBL section of a subprogram (controllers heidenhain.md 1; machine-config 2, sub_end).
    private const string SubprogramEnd = "0";

    /// <summary>
    /// Reports every LABEL and SUB section of the program that begins with the block, the walks of the subprograms it
    /// calls included, that would be written as an LBL another one of its file has, or as LBL 0 (CMP116).
    /// </summary>
    /// <param name="writing">The PROGRAM=BEGIN block being written.</param>
    // TODO(question): language 4.9 keeps LABEL unique per program or subprogram and the SUB names apart from the
    // labels, and Klartext has one LBL of each number or name per program, the LBL sections of its subprograms
    // included (controllers heidenhain.md 1; language 4.13); no document says whether the compiler may renumber the
    // labels of a section to keep them apart, nor what a LABEL=0 becomes. Each such label is reported (CMP116), and
    // nothing is renumbered.
    public static void Check(HeidenhainBlock writing)
    {
        var written = new Dictionary<string, Block>(StringComparer.Ordinal);
        var seen = new HashSet<Block>();
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int index = writing.Step.Index; index < steps.Count; index++)
        {
            // A subprogram walked twice from the program is one LBL section of its file (virtual machine 3.9, D99).
            Block block = steps[index].Block;
            if (seen.Add(block))
            {
                foreach (Word label in LabelsOf(block))
                {
                    Compare(writing, written, block, label);
                }
            }

            if (block.Has("PROGRAM", null, "END"))
            {
                return;
            }
        }
    }

    // The words of the block that are written as an LBL: LABEL=n, and the NAME of SUB=BEGIN (controller-mapping 6,
    // LABEL; heidenhain 8 rule 6).
    private static List<Word> LabelsOf(Block block)
    {
        var labels = new List<Word>();
        if (block.Find("LABEL") is Word label)
        {
            labels.Add(label);
        }

        if (block.Has("SUB", null, "BEGIN") && block.Find("NAME") is Word name)
        {
            labels.Add(name);
        }

        return labels;
    }

    // The LBL of a label must name it alone in its file, and never be LBL 0, the end of a subprogram.
    private static void Compare(HeidenhainBlock writing, Dictionary<string, Block> written, Block block, Word label)
    {
        string text = HeidenhainFlow.Label(label.Value);
        if (text == SubprogramEnd)
        {
            writing.Diagnostics.Error(block, DiagnosticCodes.HeidenhainLabelNotUnique,
                $"{label.ToCanonical()} would be written as LBL 0, which ends a subprogram in Klartext, and the "
                + "compiler renumbers no label (controllers heidenhain.md 1; language 4.9).");
            return;
        }

        if (written.TryGetValue(text, out Block? other))
        {
            writing.Diagnostics.Error(block, DiagnosticCodes.HeidenhainLabelNotUnique,
                $"{label.ToCanonical()} would be written as LBL {text}, as the block on line {other.Line} is: Klartext "
                + "has one LBL of each number or name in a program, the LBL sections of its subprograms included, and "
                + "the compiler renumbers no label (controllers heidenhain.md 1; language 4.9, 4.13).");
            return;
        }

        written.Add(text, block);
    }
}
