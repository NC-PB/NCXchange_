using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The radius compensation of Klartext (controllers heidenhain.md 2; controller-mapping 1, COMP; language 4.4): R0, RL
/// and RR at the end of an L block, the only block that carries them, where the control has another one. COMP applies
/// from the motion of its block on, so the COMP of a block without an L block waits for the next one, and an arc, a
/// CYCL CALL at the current position or M140 that the control would run with another compensation than the program has
/// is reported (CMP115): after a change that no L block has written, at the start of a subprogram entered with such a
/// change (virtual machine 3.9, D99), and where a jump reaches its label with another one (language 4.9). After a label
/// the L block writes R0, RL or RR only where a block from the label on states COMP, since every way into the label
/// brings the compensation of the program (language 2 rule 2; HeidenhainArrivals).
/// </summary>
internal static class HeidenhainCompensation
{
    /// <summary>
    /// The compensation the control has on the way the text runs, where the text knows it (TargetState): the one the
    /// last L block wrote, the one a change without an L block left active, or the caller's at the start of the walk of
    /// a subprogram. Where it is not known the control has the one of the program, since nothing written says
    /// otherwise. A label leaves it as it is: the way into the label from the block before it is the way the text runs,
    /// and the jumps to it are checked where both are written (HeidenhainArrivals).
    /// </summary>
    public const string ActiveKey = "COMP:ACTIVE";

    /// <summary>
    /// The compensation the last L block of the program or walk wrote, for the next L block to write it where it
    /// changes (TargetState): unknown at the start of a program and of a walk, and the mark of a label after a label
    /// where the control has the compensation of the program on the way the text runs (EnterLabel).
    /// </summary>
    public const string WrittenKey = "COMP";

    /// <summary>
    /// The radius compensation after the block for its L block to carry where the control has another one, R0, RL or
    /// RR; null where it does not change. It applies from the motion of its block on (language 4.4, COMP), and Klartext
    /// writes it at the end of an L block (controllers heidenhain.md 2; differences.md, radius compensation), so the
    /// COMP of a block without a motion is written with the next straight line or HOME.
    /// </summary>
    public static string? Word(HeidenhainBlock writing)
    {
        writing.Take(WrittenKey);
        string compensation = WordOf(writing.After.Motion.Comp);
        writing.Target.Set(ActiveKey, compensation);

        // After a label every way brings the compensation of the program, which is modal (language 2 rule 2, 4.4, 4.9),
        // so the L block writes it only where a block from the label on states COMP, and then on every way.
        if (HeidenhainArrivals.MarkedLabel(writing.Target.ActiveOf(WrittenKey)) is int label)
        {
            if (!HeidenhainArrivals.States(writing, label, writing.Step.Index, WrittenKey))
            {
                return null;
            }

            writing.Target.Set(WrittenKey, compensation);
            return compensation;
        }

        return writing.Target.Changes(WrittenKey, compensation) ? compensation : null;
    }

    /// <summary>
    /// At a label: where the text knows the compensation the control has on the way the text runs into it, and it is
    /// the one of the program, every way brings the compensation of the program (language 2 rule 2, 4.9), and the label
    /// is marked, so that the L blocks after it write R0, RL or RR only where a block from the label on states COMP
    /// (Word). Otherwise, at the start of a program or of a walk (virtual machine 3.9, D99) or after a COMP that no L
    /// block has written (language 4.4), the next L block writes the compensation of the program whatever the control
    /// has, and the jumps to the label must bring that one (HeidenhainArrivals).
    /// </summary>
    /// <returns>True where the control has the compensation of the program on the way the text runs.</returns>
    public static bool EnterLabel(HeidenhainBlock writing)
    {
        bool inStep = writing.Knows(WrittenKey) && Active(writing) == WordOf(writing.Before.Motion.Comp);
        if (inStep)
        {
            writing.Target.Set(WrittenKey, HeidenhainArrivals.MarkOf(writing.Step));
        }
        else
        {
            writing.MakeUnknown(WrittenKey);
        }

        return inStep;
    }

    /// <summary>
    /// Reports a motion that Klartext writes without an L block, an arc, a CYCL CALL at the current position or M140,
    /// where the control has another radius compensation active than the program after the block: COMP applies from
    /// the motion of its block on (language 4.4), and Klartext switches it only at the end of an L block (controllers
    /// heidenhain.md 2; differences.md, radius compensation), so the control would run the motion with the old one.
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="motion">The motion as the message names it: "The ARC".</param>
    // TODO(question): heidenhain.md 2 and differences.md write R0, RL and RR at the end of the L block only, and no
    // document says whether a C, CR or CP block, a CYCL CALL or M140 may carry them; such a motion after a COMP that
    // no L block has written is reported (CMP115) and nothing is guessed.
    public static void Check(HeidenhainBlock writing, string motion)
    {
        writing.Take(WrittenKey);
        string active = Active(writing);
        string compensation = WordOf(writing.After.Motion.Comp);
        if (active == compensation)
        {
            return;
        }

        writing.Error(DiagnosticCodes.HeidenhainCompensationWithoutLine,
            $"{motion} runs with the radius compensation {compensation} of the program, and the control still has "
            + $"{active} active: COMP applies from the motion of its block on, and Klartext switches it at the end of "
            + "an L block only, which no L block before this motion has done (language 4.4; controllers heidenhain.md "
            + "2; differences.md).");

        // One change is reported once: the motions after it are compared with the compensation of the program.
        writing.Target.Set(ActiveKey, compensation);
    }

    /// <summary>
    /// Keeps the COMP of a block without an L block for the next L block, where Klartext writes it: the control has
    /// not taken it over yet (language 4.4; controllers heidenhain.md 2). Where the target state does not know what the
    /// control has, at the start of a program or of a walk whose caller knows nothing either, the control has the
    /// compensation of the program before the change, which is recorded, so that a motion before the next L block is
    /// compared with it (Check). A COMP that no motion follows before the end of the program needs no L block: it moves
    /// nothing, and PROGRAM=END, the M30 of program_end, resets the compensation (virtual machine 4).
    /// </summary>
    public static void KeepPending(HeidenhainBlock writing)
    {
        writing.Take(WrittenKey);
        if (writing.Target.ActiveOf(ActiveKey) is null && writing.Before.Motion.Comp != writing.After.Motion.Comp)
        {
            writing.Target.Set(ActiveKey, WordOf(writing.Before.Motion.Comp));
        }
    }

    /// <summary>
    /// The walk of a subprogram that a CALL enters runs with the caller's state (virtual machine 3.9, D99), so the
    /// control has the compensation the caller left it at the CALL, which may be the one before a COMP that no L block
    /// of the caller has written; the walk is written from an unknown target state all the same, so that its first L
    /// block writes R0, RL or RR for every caller.
    /// </summary>
    /// <param name="writing">The block being written, SUB=BEGIN of a walk.</param>
    /// <param name="caller">What the control has active where the walk begins: the caller's target state at its CALL,
    /// or that of the walk before it where a CALL with TIMES walks the section again.</param>
    public static void EnterWalk(HeidenhainBlock writing, TargetState? caller)
    {
        if (writing.Block.Has("SUB", null, "BEGIN") && writing.After.Flow.Calls.Count > 0
            && caller?.ActiveOf(ActiveKey) is string active)
        {
            writing.Target.Set(ActiveKey, active);
        }
    }

    /// <summary>
    /// R0, RL and RR: the radius compensation off, left and right (controllers heidenhain.md 2; controller-mapping 1,
    /// COMP).
    /// </summary>
    public static string WordOf(Compensation compensation)
    {
        return compensation switch
        {
            Compensation.Left => "RL",
            Compensation.Right => "RR",
            _ => "R0",
        };
    }

    /// <summary>
    /// The compensation the control has, R0, RL or RR: the one the target state knows, or else the one of the program
    /// before the block, which nothing written since the start of the program has changed.
    /// </summary>
    public static string Active(HeidenhainBlock writing)
    {
        return writing.Target.ActiveOf(ActiveKey) ?? WordOf(writing.Before.Motion.Comp);
    }

    /// <summary>
    /// Tells whether Klartext writes the motion of the block as an L block, which carries R0, RL or RR: RAPID, LINE,
    /// HOME and a CYCLE_CALL at a position, L ... M99 (HeidenhainMotion, HeidenhainFrames.WriteHome,
    /// HeidenhainCycles.WriteCall).
    /// </summary>
    public static bool IsLBlock(Block block)
    {
        return block.Verb?.Key switch
        {
            "RAPID" or "LINE" or "HOME" => true,
            "CYCLE_CALL" => HeidenhainAxes.Of(block).Count > 0,
            _ => false,
        };
    }

    /// <summary>
    /// Tells whether Klartext writes the motion of the block without an L block, so that it runs with the compensation
    /// the control has: an arc, M140 of RETRACT and a CYCL CALL at the current position (Check).
    /// </summary>
    public static bool RunsWithoutLBlock(Block block)
    {
        return block.Verb?.Key is "ARC" or "RETRACT" or "CYCLE_CALL" && !IsLBlock(block);
    }
}
