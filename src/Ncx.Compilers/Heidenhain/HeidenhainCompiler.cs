using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// Writes an NCX program as iTNC 530 Klartext (controllers heidenhain.md 8; controller-mapping, the Heidenhain column;
/// architecture 8): one .h file per program between BEGIN PGM and END PGM with consecutive block numbers from 0, the
/// comma and a sign on every coordinate, L with FMAX or F, CC plus C and CR, TOOL CALL, HOME as M91 moves, CYCL DEF
/// with its Q parameters and CYCL CALL or M99, LBL sections after the M30 of every caller. Each concern of a block is a
/// file of this folder; this class writes a block through them in the order the control executes it.
/// </summary>
public sealed class HeidenhainCompiler : CompilerBase
{
    /// <summary>
    /// Heidenhain, the controller of the machine files whose programs this compiler writes (machine-config 1).
    /// </summary>
    public override Controller Controller => Controller.Heidenhain;

    /// <summary>
    /// The extension of a Klartext file, .h (controllers heidenhain.md, introduction).
    /// </summary>
    protected override string FileExtension => ".h";

    /// <summary>
    /// Klartext numbers its blocks without a letter in front, 0 BEGIN PGM (controllers heidenhain.md 1, 8 rule 1).
    /// </summary>
    protected override string BlockNumberPrefix => "";

    /// <summary>
    /// A comment block of Klartext, ; text (controllers heidenhain.md 1; controller-mapping 1, COMMENT).
    /// </summary>
    protected override string CommentLine(string text)
    {
        return HeidenhainProgramFrame.CommentLine(text);
    }

    /// <summary>
    /// Every line takes a block number except the continuation lines of a cycle definition, which belong to its first
    /// line (controllers heidenhain.md 1).
    /// </summary>
    protected override bool TakesBlockNumber(string line)
    {
        return !HeidenhainCycles.IsContinuation(line);
    }

    /// <summary>
    /// BEGIN PGM name MM (heidenhain 8 rule 1); the labels of the program and of the LBL sections of its file are
    /// checked here, where the whole program is ahead (controllers heidenhain.md 1; language 4.9, 4.13).
    /// </summary>
    protected override void WriteHeader(Section program, ChannelSnapshot state)
    {
        HeidenhainBlock writing = NewBlock();
        HeidenhainProgramFrame.WriteBegin(writing, program, state);
        HeidenhainLabels.Check(writing);
    }

    /// <summary>
    /// END PGM name MM after the M30 and the subprograms behind it (heidenhain 8 rules 1 and 6), with the units of
    /// BEGIN PGM, not those of the state at its end (controllers heidenhain.md 1).
    /// </summary>
    protected override void WriteFooter(Section program, ChannelSnapshot state)
    {
        HeidenhainProgramFrame.WriteEndOfProgram(NewBlock(), program);
    }

    /// <summary>
    /// Writes one block as Klartext: its structure and label, then its state words, each in a block of its own before
    /// the motion, then the motion, then what follows it; a word no concern writes is an ERROR (CMP101).
    /// </summary>
    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        HeidenhainBlock writing = NewBlock();

        // The structure of the file and the comments first, and a label where the block begins, the target of a jump
        // (language 4.9, 4.13).
        HeidenhainProgramFrame.WriteStart(writing);
        HeidenhainFlow.WriteLabel(writing);

        // The state words of a block act before its motion (language 5 rule 3): the frame, the tool, the modes and the
        // functions, the variables and the cycle definition, each in a Klartext block of its own.
        HeidenhainChain.Write(writing);
        HeidenhainToolCall.Write(writing);
        HeidenhainFrames.WriteModes(writing);
        HeidenhainFunctions.Write(writing);
        HeidenhainFlow.WriteVariables(writing);
        HeidenhainCycles.WriteDefinition(writing);

        // The motion of the block (language 5 rule 1).
        WriteMotion(writing);

        // After the motion: the dwell and the stop, the calls and the jumps, the end of the program or subprogram
        // (language 4.1, 4.9, 4.13).
        HeidenhainCycles.WriteDwell(writing);
        HeidenhainFunctions.WriteStop(writing);
        HeidenhainSubprograms.WriteCall(writing);
        HeidenhainFlow.WriteJump(writing);
        HeidenhainProgramFrame.WriteEnd(writing);
        HeidenhainMotion.KeepPending(writing);
        ReportUnwritten(writing);
    }

    // The verb of the block: RAPID and LINE as L or LN, ARC as CR, CC plus C or CC plus CP, HOME as the M91 move,
    // RETRACT as M140, CYCLE_CALL as CYCL CALL or M99; SHIFT, TILT and TILT_AXIS are written with the chain, and so is
    // SETPOS, folded into cycle 7 (machine-config 3, HeidenhainSetpos).
    private static void WriteMotion(HeidenhainBlock writing)
    {
        switch (writing.Block.Verb?.Key)
        {
            case "RAPID":
                writing.Take("RAPID");
                HeidenhainMotion.WriteStraight(writing, rapid: true, call: null);
                break;
            case "LINE":
                writing.Take("LINE");
                HeidenhainMotion.WriteStraight(writing, rapid: false, call: null);
                break;
            case "ARC":
                HeidenhainArcs.Write(writing);
                break;
            case "HOME":
                HeidenhainFrames.WriteHome(writing);
                break;
            case "RETRACT":
                HeidenhainFrames.WriteRetract(writing);
                break;
            case "CYCLE_CALL":
                HeidenhainCycles.WriteCall(writing);
                break;
        }
    }

    // Nothing of the program is dropped silently (language 2 rule 8): a word no concern wrote is an ERROR.
    private static void ReportUnwritten(HeidenhainBlock writing)
    {
        List<Word> unwritten = writing.Unwritten();
        if (unwritten.Count == 0)
        {
            return;
        }

        writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
            $"The Heidenhain compiler writes no Klartext for {HeidenhainBlock.WordsText(unwritten)}, and nothing is "
            + "written for it (controllers heidenhain.md 8; language 2 rule 8).");
    }

    // The block being written with what its concerns write it with.
    private HeidenhainBlock NewBlock()
    {
        return new HeidenhainBlock
        {
            Step = Step,
            Machine = Machine,
            Numbers = Numbers,
            Target = Target,
            Diagnostics = Diagnostics,
            LookAhead = LookAhead,
            Templates = Templates,
            WriteLine = Line,
            WriteToolChange = values => WriteToolChange(values),
            WritePreload = () => WritePreload(),
            CommentText = CommentText,
        };
    }
}
