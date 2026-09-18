using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The straight lines of Klartext (controllers heidenhain.md 2; 8 rule 2; controller-mapping 2): RAPID and LINE as L
/// with the axis words, the radius compensation R0, RL or RR where it changed, FMAX for the rapid and F where the feed
/// changed, M91 for FRAME=MACHINE; a line with the tool vector as LN (D81). The feed of a block without a motion waits
/// for the next motion, its compensation for the next L block, the only block that carries R0, RL and RR; a motion
/// without an L block that the compensation would reach first is reported (CheckCompensation).
/// </summary>
internal static class HeidenhainMotion
{
    // What the control has active: the radius compensation and the feed (TargetState).
    private const string CompensationKey = "COMP";
    private const string FeedKey = "F";

    // FMAX is the rapid of a straight block, M91 the machine frame of a block (controllers heidenhain.md 2).
    private const string Rapid = "FMAX";
    private const string MachineFrame = "M91";

    // LN X Y Z NX NY NZ TX TY TZ F: the surface normal, then the tool vector (controllers heidenhain.md 2; D81).
    private static readonly string[] s_vectorWords = ["NX", "NY", "NZ", "TX", "TY", "TZ"];

    /// <summary>
    /// Writes the straight motion of the block: L X+10 Y-5 R0 FMAX, L Z-10 F2387, L Z+0 FMAX M91 (controllers
    /// heidenhain.md 2; 8 rule 2).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="rapid">True for the rapid of RAPID and of a cycle call, FMAX.</param>
    /// <param name="call">The M function of a cycle call at the end of the block, M99; null for none.</param>
    public static void WriteStraight(HeidenhainBlock writing, bool rapid, string? call)
    {
        string? axes = HeidenhainAxes.Write(writing, HeidenhainAxes.Of(writing.Block));
        if (axes is null)
        {
            return;
        }

        bool vector = writing.Block.Has("TX");
        var words = new List<string> { vector ? "LN" : "L" };
        if (axes.Length > 0)
        {
            words.Add(axes);
        }

        // LN carries the surface normal and the tool vector as the unit vectors of the program (D81; language 6 writes
        // them without a sign).
        // TODO(question): D185: the vector form under RAPID is open; its recommendation gives RAPID and LINE the
        // vector form, so a RAPID with a tool vector is written LN with FMAX.
        if (vector && !AddVector(writing, words))
        {
            return;
        }

        if (CompensationWord(writing) is string compensation)
        {
            words.Add(compensation);
        }

        if (rapid)
        {
            writing.Take(FeedKey);
            words.Add(Rapid);
        }
        else if (Feed(writing) is string feed)
        {
            words.Add(feed);
        }

        // M91 in the block: the coordinates come from the machine datum (controllers heidenhain.md 2;
        // controller-mapping 1, FRAME=MACHINE).
        if (writing.Take("FRAME") is not null)
        {
            words.Add(MachineFrame);
        }

        if (call is not null)
        {
            words.Add(call);
        }

        writing.Line(string.Join(" ", words));
    }

    /// <summary>
    /// The radius compensation after the block where the control has another one active, R0, RL or RR; null where
    /// it does not change. It applies from the motion of its block on (language 4.4, COMP), and Klartext writes it at
    /// the end of an L block (controllers heidenhain.md 2; differences.md, radius compensation), so the COMP of a block
    /// without a motion is written with the next straight line or HOME, and CheckCompensation reports the motions
    /// before it that cannot carry it.
    /// </summary>
    public static string? CompensationWord(HeidenhainBlock writing)
    {
        writing.Take("COMP");
        string compensation = WordOf(writing.After.Motion.Comp);
        return writing.Target.Changes(CompensationKey, compensation) ? compensation : null;
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
    public static void CheckCompensation(HeidenhainBlock writing, string motion)
    {
        writing.Take("COMP");
        string active = ActiveCompensation(writing);
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
        writing.Target.Set(CompensationKey, compensation);
    }

    // TODO(question): heidenhain 8 rule 2 writes F "on every block by option", and machine-config 2 has no key for
    // that option; the feed is written where it changes.

    /// <summary>
    /// The feed of the block where it differs from the one the control has, F2387, F1,5; null where it does not
    /// change: F on the blocks where the feed changed (controllers heidenhain.md 8 rule 2; F modal, heidenhain 2).
    /// </summary>
    public static string? Feed(HeidenhainBlock writing)
    {
        Word? feedWord = writing.Take(FeedKey);
        if (writing.After.Unknown.Contains(FeedKey))
        {
            if (feedWord is not null)
            {
                HeidenhainNumbers.ReportValue(writing, feedWord);
            }

            return null;
        }

        if (writing.After.Motion.Feed is not decimal feed)
        {
            return null;
        }

        string text = FeedKey + writing.Numbers.FormatFeed(feed, writing.Block);
        return writing.Target.Changes(FeedKey, text) ? text : null;
    }

    /// <summary>
    /// Keeps the F and the COMP of a block without their motion for the next motion, where Klartext writes them: the
    /// control has not taken them over yet, so the next line or arc writes the feed and the next L block the
    /// compensation where they differ (language 4.3, 4.4; controllers heidenhain.md 2). Where the target state does not
    /// know the compensation yet, at the start of a program or of the walk of a subprogram, the control has the one of
    /// the program before the change, which is recorded, so that a motion before the next L block is compared with it
    /// (CheckCompensation; virtual machine 3.9, D99: a walk runs with the state of its caller). A COMP that no motion
    /// follows before the end of the program needs no L block: it moves nothing, and PROGRAM=END, the M30 of
    /// program_end, resets the compensation (virtual machine 4).
    /// </summary>
    public static void KeepPending(HeidenhainBlock writing)
    {
        writing.Take(FeedKey);
        writing.Take("COMP");
        if (writing.Target.ActiveOf(CompensationKey) is null && writing.Before.Motion.Comp != writing.After.Motion.Comp)
        {
            writing.Target.Set(CompensationKey, WordOf(writing.Before.Motion.Comp));
        }
    }

    // R0, RL and RR: the radius compensation off, left and right (controllers heidenhain.md 2; controller-mapping 1,
    // COMP).
    private static string WordOf(Compensation compensation)
    {
        return compensation switch
        {
            Compensation.Left => "RL",
            Compensation.Right => "RR",
            _ => "R0",
        };
    }

    // The radius compensation the control has active before the block: the one written last, or, where the target
    // state does not know it, the one of the program before the block, which nothing written since the start of the
    // program or of the walk has changed (KeepPending records a change without an L block).
    private static string ActiveCompensation(HeidenhainBlock writing)
    {
        return writing.Target.ActiveOf(CompensationKey) ?? WordOf(writing.Before.Motion.Comp);
    }

    // The surface normal and the tool vector of LN, each component as the program writes it.
    private static bool AddVector(HeidenhainBlock writing, List<string> words)
    {
        foreach (string key in s_vectorWords)
        {
            if (writing.Take(key) is not Word component)
            {
                continue;
            }

            if (HeidenhainNumbers.NumberOf(component.Value) is not decimal value)
            {
                HeidenhainNumbers.ReportValue(writing, component);
                return false;
            }

            words.Add(key + writing.Numbers.Format(key, value, writing.Block));
        }

        return true;
    }
}
