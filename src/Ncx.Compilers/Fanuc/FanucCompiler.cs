using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// Writes an NCX program as a Fanuc program (controllers fanuc.md 10; controller-mapping, the Fanuc column): % around
/// the file, O with the name, the start block of the machine, a modal G only on change, T and M6 per [tool_change], M3
/// with S in one block, G43 H and D where the offsets stand, the frames, the canned cycles with R and Z from the
/// absolute planes, the flow of custom macro B, the wait marks of [sync], program_end, and the subprograms as O
/// programs after the end. One concern per file: FanucProgramFrame, FanucMotion, FanucToolWords, FanucFunctions,
/// FanucFrames, FanucCycles, FanucFlow, FanucSync.
/// </summary>
public sealed class FanucCompiler : CompilerBase
{
    // The labels of each program and subprogram of the file being compiled, as block numbers.
    private readonly Dictionary<Section, FanucLabels> _labels = [];

    // The skipped blocks whose changes to the target state wait to be made unknown: the target state of the program or
    // walk of the block, and what it held before the block.
    private readonly List<(TargetState Target, Dictionary<string, string> Before)> _skipped = [];

    /// <summary>
    /// The Fanuc family, Fanuc and the ISO dialects (controllers fanuc.md).
    /// </summary>
    public override Controller Controller => Controller.Fanuc;

    /// <summary>
    /// A Fanuc program is an .nc file (machine-config 10).
    /// </summary>
    protected override string FileExtension => ".nc";

    /// <summary>
    /// The decimal point stands on every real number, X70. (controllers fanuc.md 2, 10 rule 5).
    /// </summary>
    protected override bool DecimalPointOnWholeNumbers => true;

    /// <summary>
    /// A comment in parentheses (controllers fanuc.md 1, 10 rule 5).
    /// </summary>
    protected override string CommentLine(string text)
    {
        return "(" + text + ")";
    }

    /// <summary>
    /// The % and O lines, the comment lines, the lines of a skipped block and the label lines, which carry their own
    /// N, take no block number of block_numbers (controllers fanuc.md 1; machine-config 2).
    /// </summary>
    protected override bool TakesBlockNumber(string line)
    {
        if (line.Length == 0)
        {
            return true;
        }

        bool numbered = line.Length > 1 && char.IsAsciiDigit(line[1]);
        return !(line[0] is '%' or '(' or '/' || (line[0] is 'O' or 'N' && numbered));
    }

    /// <summary>
    /// The number a label line carries itself, N20, and the end that a conditional JUMP=END names, N6 M30: block
    /// numbers matter as jump targets (controllers fanuc.md 1; controller-mapping 6, LABEL).
    /// </summary>
    // TODO(question): the documents do not say how the numbers of the labels share the N numbers of a program with
    // block_numbers (controllers fanuc.md 1, 10 rule 6; machine-config 2; controller-mapping 6, LABEL), and a number
    // that stands twice makes the target of GOTO ambiguous; block_numbers gives no line the number of a label of the
    // file and goes on past it, until that is answered.
    protected override int? OwnBlockNumber(string line)
    {
        int end = 1;
        while (end < line.Length && char.IsAsciiDigit(line[end]))
        {
            end++;
        }

        bool numbered = line.Length > 1 && line[0] == 'N' && end > 1;
        return numbered && int.TryParse(line.AsSpan(1, end - 1), NumberStyles.None, CultureInfo.InvariantCulture,
            out int number)
            ? number
            : null;
    }

    /// <summary>
    /// Writes one block: the structure of the file, RAW, and every other block concern by concern in the order the
    /// control executes them, state before motion (language 5 rule 3): the label, the transforms, the tool change,
    /// the spindle settings, the main line with the modal codes, the motion or the cycle and the functions, then the
    /// dwell, the flow, the wait mark and the comment.
    /// </summary>
    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        if (block.Has("FILE", null, "BEGIN"))
        {
            _labels.Clear();
            _skipped.Clear();
        }

        var write = new FanucBlock
        {
            Step = Step,
            Steps = LookAhead.Steps,
            Machine = Machine,
            Target = Target,
            Numbers = Numbers,
            Templates = Templates,
            Diagnostics = Diagnostics,
            Labels = LabelsOf(Step.Section),
            Writer = Line,
            CommentText = CommentText,
        };
        MakeSkippedUnknown(write);
        if (!FanucProgramFrame.Write(write) && !WriteNative(write))
        {
            WriteWords(write);
        }

        write.ReportUnwritten();
    }

    // RAW of this controller or of the machine's builder is written as it is (language 4.1, D5); the definition of a
    // native cycle waits for its CYCLE_CALL (language 4.7.1, D94).
    private static bool WriteNative(FanucBlock write)
    {
        if (RawText(write.Block) is string raw)
        {
            // RAW may move the tool axis, so a length offset that waits for it stands before it.
            write.Written("RAW");
            write.Written("SKIP");
            FanucToolWords.WriteWaitingLength(write);
            write.Write(raw);
            FanucProgramFrame.WriteComment(write);
            return true;
        }

        if (NativeCycleOf(write.Block) is not null)
        {
            foreach (Word word in write.Block.Words)
            {
                write.Written(word);
            }

            return true;
        }

        return false;
    }

    private void WriteWords(FanucBlock write)
    {
        write.Written("SKIP");
        if (TakesWaitingLengthBefore(write))
        {
            FanucToolWords.WriteWaitingLength(write);
        }

        if (write.Block.Skip)
        {
            _skipped.Add((Target, new Dictionary<string, string>(Target.Active, StringComparer.Ordinal)));
        }

        FanucFlow.WriteLabel(write);
        FanucFrames.WriteChain(write);
        WriteTools(write);
        FanucToolWords.WriteSpindleLines(write);
        FanucFrames.WriteTransformations(write);
        FanucMotion.WriteModalWords(write);
        FanucCycles.Write(write);
        FanucMotion.Write(write);
        FanucFrames.WriteVerbs(write);
        FanucToolWords.AddOffsets(write, movesToolAxis: false);
        FanucFunctions.Write(write);
        write.WriteMain();
        FanucMotion.WriteDwell(write);
        FanucFlow.Write(write);
        FanucSync.Write(write);
        FanucProgramFrame.WriteComment(write);
    }

    // A path may run the lines after the block without those before it, or the lines of the block not at all: a LABEL
    // that a jump reaches, a JUMP, a CALL of a subprogram written from an unknown target state (D99), a skipped block
    // that moves the tool axis or sets the length offset itself (controller-mapping 1, SKIP); a length offset that
    // waits for the tool axis stands before it (language 4.4, OFFSET:LEN is modal; the TODO(question) of
    // OutputFormat).
    private static bool TakesWaitingLengthBefore(FanucBlock write)
    {
        Block block = write.Block;
        bool setsLength = block.Find("OFFSET", "LEN") is not null || block.Has("TCPM");
        return block.Has("LABEL") || block.Has("JUMP") || block.Has("CALL")
            || (block.Skip && (FanucMotion.MovesToolAxis(write) || setsLength));
    }

    // The lines of a skipped block reach the control only while the block-skip switch is off (controller-mapping 1,
    // SKIP), so every code and value the block changed in the target state is unknown after it, and the next block of
    // its program or walk writes its own again: NCX states the verb and the absolute or incremental words on every
    // block (language 2 rules 2 and 3). A skipped CALL is made unknown after its subprogram has returned, with what the
    // walk wrote (virtual machine 3.9, D99).
    private void MakeSkippedUnknown(FanucBlock write)
    {
        for (int index = _skipped.Count - 1; index >= 0; index--)
        {
            (TargetState target, Dictionary<string, string> before) = _skipped[index];
            if (!ReferenceEquals(target, Target))
            {
                continue;
            }

            _skipped.RemoveAt(index);
            var keys = new HashSet<string>(before.Keys, StringComparer.Ordinal);
            keys.UnionWith(target.Active.Keys);
            foreach (string key in keys)
            {
                bool kept = before.TryGetValue(key, out string? value) && target.ActiveOf(key) == value;
                if (!kept && key != FanucToolWords.PendingLength)
                {
                    write.MakeUnknown(key);
                }
            }
        }
    }

    // The tool change and the preload are written from [tool_change] by the framework (machine-config 3, virtual
    // machine 3.5); a canned cycle of the control ends first (virtual machine 4, cycle row).
    private void WriteTools(FanucBlock write)
    {
        bool change = write.Block.Has("TOOL");
        bool preload = write.Block.Has("PRELOAD");
        if (!change && !preload)
        {
            return;
        }

        if (write.Block.Skip)
        {
            write.Error(DiagnosticCodes.FanucSkipOnToolChange,
                "SKIP on a block with TOOL or PRELOAD: the tool change is written from [tool_change] without the "
                + "skip mark /, so the block cannot be written as skipped (controller-mapping 1, SKIP).");
        }

        if (change)
        {
            FanucCycles.CancelBeforeToolChange(write);
            WriteToolChange();
            FanucToolWords.AfterChange(write);
        }

        if (preload)
        {
            WritePreload();
        }

        write.Written("TOOL");
        write.Written("PRELOAD");
    }

    private FanucLabels LabelsOf(Section? section)
    {
        if (section is null)
        {
            return FanucLabels.None;
        }

        if (!_labels.TryGetValue(section, out FanucLabels? labels))
        {
            labels = FanucLabels.Of(section, LookAhead.Steps);
            _labels.Add(section, labels);
        }

        return labels;
    }
}
