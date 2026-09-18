using Ncx.Core.Model;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The labels of one program or subprogram as Fanuc block numbers (controllers fanuc.md 1, 7; controller-mapping 6,
/// LABEL): a numeric LABEL is its own block number, the target of GOTO; the label after the header that the loop of
/// M99 returns to (D210); the block number of the end where a conditional JUMP=END goes.
/// </summary>
internal sealed class FanucLabels
{
    // The words of the header block of a program (D34): the label of the M99 loop stands right after them.
    private static readonly string[] s_headerKeys = ["FEED_MODE", "COMP", "UNITS", "WORKPLANE", "DIAMETER", "CYCLE"];

    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);

    // The labels a JUMP of the section names.
    private readonly HashSet<string> _jumpTargets = new(StringComparer.Ordinal);

    /// <summary>
    /// The labels of no section: FILE=BEGIN and FILE=END.
    /// </summary>
    public static FanucLabels None { get; } = new();

    /// <summary>
    /// The label after the header whose every jump is unconditional, written as M99 (D210); null for none.
    /// </summary>
    public string? LoopLabel { get; private set; }

    /// <summary>
    /// The block number of the end of the program that a conditional JUMP=END names; null when no such jump stands
    /// in it.
    /// </summary>
    public int? EndLabel { get; private set; }

    /// <summary>
    /// The block number of a label; null for a label the section does not have.
    /// </summary>
    /// <param name="label">The label as NCX writes it, "300", "WHILE_12".</param>
    public int? NumberOf(string label)
    {
        return _numbers.TryGetValue(label, out int number) ? number : null;
    }

    /// <summary>
    /// True for a label that a JUMP of the section names, which the control may reach from that jump as well as from
    /// the block before it (language 4.9, JUMP).
    /// </summary>
    /// <param name="label">The label as NCX writes it.</param>
    public bool IsJumpTarget(string label)
    {
        return _jumpTargets.Contains(label);
    }

    /// <summary>
    /// The labels of a section from the blocks of its walks.
    /// </summary>
    /// <param name="section">The program or subprogram.</param>
    /// <param name="steps">Every step of the run.</param>
    public static FanucLabels Of(Section section, IReadOnlyList<BlockStep> steps)
    {
        var labels = new FanucLabels();
        var blocks = new List<Block>();
        foreach (BlockStep step in steps)
        {
            if (step.Section == section && !blocks.Contains(step.Block))
            {
                blocks.Add(step.Block);
            }
        }

        blocks.Sort((first, second) => first.Line.CompareTo(second.Line));
        labels.Number(blocks);
        labels.FindLoop(section, blocks);
        return labels;
    }

    // A numeric LABEL is the block number N that GOTO names (controllers fanuc.md 1, 7). A LABEL named by an
    // identifier, as a reader lowers WHILE and IF THEN (controller-mapping 6), has no Fanuc form.
    // TODO(question): a LABEL named by an identifier (WHILE_12, IF_7) and the end that a conditional JUMP=END names
    // need a block number that no document gives; they take the numbers after the greatest numeric label of their
    // section, in the order they stand, the end last.
    private void Number(List<Block> blocks)
    {
        var identifiers = new List<string>();
        int greatest = 0;
        bool conditionalEnd = false;
        foreach (Block block in blocks)
        {
            if (block.Find("LABEL")?.Value is IntegerValue number)
            {
                _numbers[number.Text] = (int)number.Number;
                greatest = Math.Max(greatest, (int)number.Number);
            }
            else if (block.Find("LABEL")?.Value is IdentValue name && !identifiers.Contains(name.Name))
            {
                identifiers.Add(name.Name);
            }

            conditionalEnd |= block.Has("JUMP", null, "END") && block.Has("IF");
            if (block.Find("JUMP") is Word jump)
            {
                _jumpTargets.Add(jump.Value.ToCanonical());
            }
        }

        foreach (string identifier in identifiers)
        {
            _numbers[identifier] = ++greatest;
        }

        EndLabel = conditionalEnd ? greatest + 1 : null;
    }

    // The loop of a main program: M99 jumps back to its first block, which the reader writes as a LABEL after the
    // header and a JUMP to it (controller-mapping 6, M99 paragraph).
    // TODO(question): D210: the compiler writes a JUMP to the label after the header back as M99 where every jump to it
    // is unconditional, as D210 recommends, until D210 is answered.
    private void FindLoop(Section section, List<Block> blocks)
    {
        if (section.Kind != SectionKind.Program || blocks.Count < 2)
        {
            return;
        }

        int index = 1;
        if (index < blocks.Count && IsHeader(blocks[index]))
        {
            index++;
        }

        if (index >= blocks.Count || blocks[index].Words.Count != 1 || blocks[index].Find("LABEL") is not Word label)
        {
            return;
        }

        string name = label.Value.ToCanonical();
        foreach (Block block in blocks)
        {
            if (block.Find("JUMP")?.Value.ToCanonical() == name && block.Has("IF"))
            {
                return;
            }
        }

        LoopLabel = name;
    }

    // The header block of D34: state words only.
    private static bool IsHeader(Block block)
    {
        foreach (Word word in block.Words)
        {
            if (!s_headerKeys.Contains(word.Key))
            {
                return false;
            }
        }

        return block.Words.Count > 0;
    }
}
