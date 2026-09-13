using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The validation list of virtual machine 5 during one run, where the rules that block execution and motion do not
/// raise themselves are checked: the pre-pass over the file before execution, the rules of each block around its
/// steps, and the count of the expressions STATIC mode leaves unresolved at the end. One per run, because some rules
/// remember what the run found: the offset form of a program, the unresolved expressions.
/// </summary>
internal sealed class RunValidation
{
    private readonly MachineConfig _machine;
    private readonly Diagnostics _diagnostics;
    private readonly ExecutionMode _mode;

    // Only a machine with a linear axis that has limits compares the targets of its motions (virtual machine 5, D100).
    private readonly bool _comparesLimits;

    // The line where the program that runs gave each form of the offsets first (ToolValidation).
    private readonly Dictionary<string, int> _offsetForms = new(StringComparer.Ordinal);

    // The expressions STATIC mode left unresolved, each once, and the block of the first (virtual machine 1, 5).
    private readonly HashSet<Word> _unresolved = new(ReferenceEqualityComparer.Instance);
    private Block? _firstUnresolved;

    // The state the block finds, for the rules that compare it with the state the block leaves.
    private Compensation _compensationBefore;
    private string? _workpieceHolderBefore;
    private Dictionary<string, AxisPosition>? _positionsBefore;

    /// <summary>
    /// The validation of one run.
    /// </summary>
    /// <param name="machine">The machine file, or the built-in default machine of D103.</param>
    /// <param name="diagnostics">The diagnostics of the run.</param>
    /// <param name="mode">STATIC or INTERPRETED (virtual machine 1).</param>
    public RunValidation(MachineConfig machine, Diagnostics diagnostics, ExecutionMode mode)
    {
        _machine = machine;
        _diagnostics = diagnostics;
        _mode = mode;
        _comparesLimits = MotionValidation.HasLinearLimits(machine);
    }

    /// <summary>
    /// The pre-pass over the file before execution (virtual machine 3.6): the labels and jump targets of every program
    /// and subprogram, whose ERRORs stop the run before its first block, and the rules about a block as it is written,
    /// reported once per block however often the walks pass it: JUMP=END from a subprogram, RETURN in a program, the
    /// unreachable blocks, SYNC in a single-channel job, RAW, F in a RAPID block, MFUNC and ROT against the machine.
    /// </summary>
    public void CheckFile(NcxProgram program)
    {
        bool singleChannelJob = ChannelValidation.IsSingleChannelJob(program);
        foreach (Section section in program.Sections)
        {
            FlowValidation.CheckSection(program, section, _diagnostics);
            for (int index = section.FirstBlock; index <= section.LastBlock; index++)
            {
                Block block = program.Blocks[index];
                StructureValidation.CheckRaw(block, _diagnostics);
                FrameValidation.CheckRot(block, _machine, _diagnostics);
                MotionValidation.CheckFeedInRapid(block, _diagnostics);
                ResourceValidation.CheckMfunc(block, _machine, _diagnostics);
                ChannelValidation.CheckSync(block, singleChannelJob, _diagnostics);
            }
        }
    }

    /// <summary>
    /// Before the state words of a block (virtual machine 3 step 3): what the rules after the block compare with, and
    /// the expressions of the block, which STATIC mode does not evaluate (virtual machine 1).
    /// </summary>
    public void BeforeBlock(BlockContext context)
    {
        Block block = context.Block;
        ChannelState state = context.State;

        // A program gives its offsets in one form; a new program starts without one (virtual machine 5).
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            _offsetForms.Clear();
        }

        _compensationBefore = state.Motion.Comp;
        _workpieceHolderBefore = state.Frame.WorkpieceHolder;
        _positionsBefore = _comparesLimits && MotionRules.IsMotion(block)
            ? new Dictionary<string, AxisPosition>(state.Motion.Position)
            : null;

        if (_mode == ExecutionMode.Static)
        {
            ExpressionValidation.CollectUnresolved(block, _unresolved);
            if (_firstUnresolved is null && _unresolved.Count > 0)
            {
                _firstUnresolved = block;
            }
        }
    }

    /// <summary>
    /// After the verb of a block (virtual machine 3 steps 4 and 5): the rules that read the state the block leaves, in
    /// the order of the families.
    /// </summary>
    public void AfterBlock(BlockContext context)
    {
        FrameValidation.CheckSetposNamesAnAxis(context);
        FrameValidation.CheckToleranceWords(context);
        MotionValidation.CheckFeedAboveMaxFeed(context);
        if (_positionsBefore is not null)
        {
            MotionValidation.CheckLimits(context, _positionsBefore);
        }

        ArcValidation.CheckCompensationChange(context, _compensationBefore);
        ToolValidation.CheckOffsetForms(context, _offsetForms);
        SpindleValidation.CheckSpindleBeforeLine(context);
        SpindleValidation.CheckRpmLimits(context);
        ResourceValidation.CheckWorkpieceChange(context, _workpieceHolderBefore);
    }

    /// <summary>
    /// At the end of a run that no ERROR stopped: the unresolved expressions of STATIC mode, counted and reported once
    /// (virtual machine 5).
    /// </summary>
    public void EndRun()
    {
        if (_firstUnresolved is Block first)
        {
            ExpressionValidation.ReportUnresolved(_unresolved.Count, first, _diagnostics);
        }
    }
}
