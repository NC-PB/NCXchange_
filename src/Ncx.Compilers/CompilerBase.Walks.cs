using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers;

// The write pass (architecture 8; virtual machine 3.9, D99): the steps in walk order, a program into its own lines, a
// subprogram once per walk from an unknown target state, of which the first walk gives the text, and BLOCK_WRITE with
// the lines of every block before they reach the output.
public abstract partial class CompilerBase
{
    // The parts being written, the innermost last: a program, and the walks of the subprograms it enters (D99).
    private readonly List<WriteContext> _contexts = [];
    private readonly List<ProgramText> _programs = [];
    private readonly Dictionary<Section, SubText> _subs = [];

    // The lines of FILE=BEGIN and FILE=END, which stand at the start and the end of every output file.
    private readonly List<OutputLine> _fileBegin = [];
    private readonly List<OutputLine> _fileEnd = [];

    // The tools whose {kind} took the default in the walk of a subprogram that no program calls (D10, D99).
    private readonly List<ToolRef> _toolsWithoutDataOutsidePrograms = [];

    // What the target control has active while FILE=BEGIN and FILE=END are written, outside every program.
    private TargetState _fileTarget = new();

    // The preloads of auto_preload that wait for the first motion after their change (machine-config 3).
    private readonly List<PendingPreload> _pendingPreloads = [];

    private WriteContext? CurrentContext => _contexts.Count > 0 ? _contexts[_contexts.Count - 1] : null;

    // A compiler may compile more than one file; each starts from nothing written.
    private void ResetWrite()
    {
        _contexts.Clear();
        _programs.Clear();
        _subs.Clear();
        _fileBegin.Clear();
        _fileEnd.Clear();
        _toolsWithoutDataOutsidePrograms.Clear();
        _fileTarget = new TargetState();
        _pendingPreloads.Clear();
        _step = null;
        _lines = null;
    }

    // Every step in walk order: FILE=BEGIN and FILE=END into the frame of every file, the blocks of a program into the
    // program, the blocks of a subprogram into the walk that entered it.
    private void WriteSteps()
    {
        foreach (BlockStep step in _steps)
        {
            Block block = step.Block;
            if (step.Section is not Section section)
            {
                List<OutputLine> frame = block.Has("FILE", null, "BEGIN") ? _fileBegin : _fileEnd;
                frame.AddRange(OutputLinesOf(WriteStep(step, null), block));
                continue;
            }

            if (block.Has("PROGRAM", null, "BEGIN"))
            {
                BeginProgram(step, section);
            }
            else if (block.Has("SUB", null, "BEGIN"))
            {
                BeginWalk(step, section);
            }

            WriteContext context = CurrentContext
                ?? throw new InvalidOperationException("A block of a section is written outside its section.");
            context.Add(WriteStep(step, section), block);
            if (context.Sub is null && block.Has("PROGRAM", null, "BEGIN") && context.Program is ProgramText program)
            {
                program.HeadEnd = context.Lines.Count;
            }

            if (block.Has("SUB", null, "END"))
            {
                EndWalk(step);
            }
            else if (block.Has("PROGRAM", null, "END"))
            {
                EndProgram(step, section);
            }
        }

        _step = null;
    }

    // The lines of one block: the header of a program in front of its PROGRAM=BEGIN, a preload that waits for this
    // block, the block as the controller writes it, the preload auto_preload inserts after a change (architecture 8,
    // machine-config 3); then BLOCK_WRITE with the lines before they reach the output (virtual machine 7).
    private List<string> WriteStep(BlockStep step, Section? section)
    {
        _step = step;
        _lines = [];
        if (section is not null && step.Block.Has("PROGRAM", null, "BEGIN"))
        {
            WriteHeader(section, LookAhead.HeaderState(section));
        }

        WritePendingPreload(step);
        WriteBlock(step.Block, step.Before, step.After);
        WriteAutoPreload(step);
        List<string> lines = _lines;
        _lines = null;
        return RaiseBlockWrite(step, lines);
    }

    // BLOCK_WRITE is raised before the lines of a block are written, the lines open to change: every block writer of
    // the plugins in its order edits them in place (virtual machine 7, architecture 8, D106). A line broken with a line
    // break becomes two lines.
    // TODO(question): virtual machine 7 gives BLOCK_WRITE "the block's words, mutable", and says nothing of what a
    // change to them does once the compiler has written the lines. A block writer receives a copy of the words to
    // read, and only the lines it leaves are written, until that is answered.
    private List<string> RaiseBlockWrite(BlockStep step, List<string> lines)
    {
        if (Options.BlockWriters.Count == 0)
        {
            return lines;
        }

        var words = new List<Word>(step.Block.Words);
        BlockWriteEvent blockWrite = step.BlockWrite is BlockWriteEvent raised
            ? raised with { Words = words, OutputLines = lines }
            : new BlockWriteEvent
            {
                Channel = step.After.ChannelId,
                Block = step.Block,
                Before = step.Before,
                After = step.After,
                Words = words,
                OutputLines = lines,
            };
        foreach (IBlockWriter writer in Options.BlockWriters)
        {
            writer.Write(blockWrite);
        }

        var edited = new List<string>();
        foreach (string line in blockWrite.OutputLines)
        {
            edited.AddRange(line.Split('\n'));
        }

        return edited;
    }

    // A program is written from an unknown target state: nothing is known of what the control has active when it
    // starts (phase 3, P3-03).
    private void BeginProgram(BlockStep step, Section section)
    {
        var program = new ProgramText { Program = section, Begin = step };
        _programs.Add(program);
        _contexts.Clear();
        _contexts.Add(new WriteContext { Target = new TargetState(), Lines = program.Lines, Program = program });
    }

    // WriteFooter writes what follows the subprograms placed after the program, END PGM on Heidenhain (architecture 8,
    // controllers heidenhain.md 8 rule 1); its lines pass BLOCK_WRITE as lines of PROGRAM=END.
    private void EndProgram(BlockStep step, Section section)
    {
        _lines = [];
        WriteFooter(section, step.After);
        List<string> footer = _lines;
        _lines = null;
        if (footer.Count > 0 && CurrentContext?.Program is ProgramText program)
        {
            program.Footer.AddRange(OutputLinesOf(RaiseBlockWrite(step, footer), step.Block));
        }

        _contexts.Clear();
    }

    // Every walk of a subprogram is written from an unknown target state, so that every modal word stands at its first
    // use inside it and the text is right for every caller (virtual machine 3.9, D99).
    private void BeginWalk(BlockStep step, Section section)
    {
        _contexts.Add(new WriteContext
        {
            Target = new TargetState(),
            Lines = [],
            Program = CurrentContext?.Program,
            Sub = section,
            Call = CallOf(step),
        });
    }

    // Each SUB section is written once, not once per CALL: its first walk gives the text, and a walk that would write
    // different lines is an ERROR naming the section and the calls (virtual machine 3.9, D99). After the return the
    // control has active what the walk wrote.
    private void EndWalk(BlockStep step)
    {
        WriteContext walk = CurrentContext ?? throw new InvalidOperationException("SUB=END without its walk.");
        _contexts.RemoveAt(_contexts.Count - 1);
        Section sub = walk.Sub ?? throw new InvalidOperationException("SUB=END ends a program.");
        if (_subs.TryGetValue(sub, out SubText? first))
        {
            CompareWalks(first, walk, step);
        }
        else
        {
            _subs.Add(sub, new SubText { Sub = sub, Lines = walk.Lines, FirstCall = walk.Call });
        }

        if (walk.Program is ProgramText program && !program.CalledSubs.Contains(sub))
        {
            program.CalledSubs.Add(sub);
        }

        CurrentContext?.Target.TakeOver(walk.Target);
    }

    // Two walks of one subprogram must write the same lines, since the section is written once (D99).
    private void CompareWalks(SubText first, WriteContext walk, BlockStep step)
    {
        int count = Math.Max(first.Lines.Count, walk.Lines.Count);
        for (int index = 0; index < count; index++)
        {
            string firstLine = index < first.Lines.Count ? first.Lines[index].Text : "no line";
            string walkLine = index < walk.Lines.Count ? walk.Lines[index].Text : "no line";
            if (firstLine == walkLine)
            {
                continue;
            }

            Diagnostics.Error(walk.Call ?? step.Block, DiagnosticCodes.SubprogramWalksDiffer,
                $"SUB {first.Sub.Name} is written once for every caller, but its walk {WalkName(walk.Call)} writes "
                + $"\"{walkLine}\" where its walk {WalkName(first.FirstCall)} writes \"{firstLine}\": the text of "
                + "a word that depends on the caller cannot stand in the section (virtual machine 3.9, D99).");
            return;
        }
    }

    // The CALL block of the walk a step belongs to, the innermost call (virtual machine 2.7); null outside a call.
    private Block? CallOf(BlockStep step)
    {
        IReadOnlyList<CallFrame> calls = step.After.Flow.Calls;
        NcxProgram program = _program ?? throw NotCompiling();
        if (calls.Count == 0)
        {
            return null;
        }

        int callIndex = calls[0].ReturnPc - 1;
        return callIndex >= 0 && callIndex < program.Blocks.Count ? program.Blocks[callIndex] : null;
    }

    private static string WalkName(Block? call)
    {
        return call is null ? "from the default entry state" : $"from the CALL on line {call.Line}";
    }

    private static List<OutputLine> OutputLinesOf(IReadOnlyList<string> lines, Block block)
    {
        var outputLines = new List<OutputLine>();
        foreach (string line in lines)
        {
            outputLines.Add(new OutputLine(line, block));
        }

        return outputLines;
    }
}
