using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The straight lines of Klartext (controllers heidenhain.md 2; 8 rule 2; controller-mapping 2): RAPID and LINE as L
/// with the axis words, the radius compensation R0, RL or RR where it changed (HeidenhainCompensation), FMAX for the
/// rapid and F where the feed changed, F500 or FQ1 of a Q parameter, M91 for FRAME=MACHINE; a line with the tool vector
/// as LN (D81). The feed of a block without a motion waits for the next motion. After a label F is written only where a
/// block from the label on states it, since every way into the label brings the feed of the program, which is modal
/// (language 2 rule 2; HeidenhainArrivals).
/// </summary>
internal static class HeidenhainMotion
{
    /// <summary>
    /// What the control has active under this key of the target state: the feed as F wrote it, F500 or the Q parameter
    /// with the step that read it (HeidenhainParameters.Active); the mark of a label after a label where the control
    /// has the feed of the program on the way the text runs (EnterLabel).
    /// </summary>
    public const string FeedKey = "F";

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

        if (HeidenhainCompensation.Word(writing) is string compensation)
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

    // TODO(question): heidenhain 8 rule 2 writes F "on every block by option", and machine-config 2 has no key for
    // that option; the feed is written where it changes.

    /// <summary>
    /// The feed of the block where it differs from the one the control has, F2387, F1,5, FQ1; null where it does not
    /// change: F on the blocks where the feed changed (controllers heidenhain.md 8 rule 2; F modal, heidenhain 2).
    /// </summary>
    public static string? Feed(HeidenhainBlock writing)
    {
        writing.Take(FeedKey);

        // After a label every way brings the feed of the program, which is modal (language 2 rule 2, 4.9), so the
        // motion writes F only where a block from the label on states it, and then on every way.
        if (HeidenhainArrivals.MarkedLabel(writing.Target.ActiveOf(FeedKey)) is int label)
        {
            if (!HeidenhainArrivals.States(writing, label, writing.Step.Index, FeedKey))
            {
                return null;
            }

            writing.MakeUnknown(FeedKey);
        }

        if (writing.After.Unknown.Contains(FeedKey))
        {
            return ParameterFeed(writing);
        }

        if (writing.After.Motion.Feed is not decimal feed)
        {
            return null;
        }

        string text = FeedKey + writing.Numbers.FormatFeed(feed, writing.Block);
        return writing.Target.Changes(FeedKey, text) ? text : null;
    }

    /// <summary>
    /// At a label: where the control has the feed of the program on the way the text runs into it, every way brings the
    /// feed of the program (language 2 rule 2, 4.9), and the label is marked, so that the motions after it write F only
    /// where a block from the label on states it (Feed). Otherwise, at the start of a program or of a walk (virtual
    /// machine 3.9, D99) or after an F that no motion has written (language 4.3), the next feed motion writes the feed
    /// of the program whatever the control has, and the jumps to the label must bring that one (HeidenhainArrivals).
    /// </summary>
    /// <returns>True where the control has the feed of the program on the way the text runs.</returns>
    public static bool EnterLabel(HeidenhainBlock writing)
    {
        int before = writing.Step.Index - 1;
        bool inStep = before >= 0 && InStep(writing, writing.Target.ActiveOf(FeedKey), before);
        if (inStep)
        {
            writing.Target.Set(FeedKey, HeidenhainArrivals.MarkOf(writing.Step));
        }
        else
        {
            writing.MakeUnknown(FeedKey);
        }

        return inStep;
    }

    /// <summary>
    /// Tells whether the control, with what the target state keeps under F, has the feed the program has after the
    /// step: after a label that no block since has stated F, the one each way brought; the number F wrote, as [format]
    /// writes the feed of the program (machine-config 2); or the Q parameter read where it had the value NCX read at
    /// the word that set the feed (HeidenhainParameters.Holds).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="active">What the target state keeps under F.</param>
    /// <param name="index">The step after which the program has the feed.</param>
    public static bool InStep(HeidenhainBlock writing, string? active, int index)
    {
        if (HeidenhainArrivals.MarkedLabel(active) is int label)
        {
            return !HeidenhainArrivals.States(writing, label, index, FeedKey);
        }

        BlockStep step = writing.LookAhead.Steps[index];
        if (step.After.Unknown.Contains(FeedKey))
        {
            return ParameterOf(writing, index) is string parameter
                && HeidenhainParameters.Holds(writing, active, parameter, afterVariables: true, index);
        }

        if (step.After.Motion.Feed is not decimal feed || !TryNumberOf(writing, active, out decimal written))
        {
            return false;
        }

        // A feed with more decimals than [format] takes is written rounded (machine-config 2; NumberFormatter.Format).
        return writing.Numbers.DecimalsOf(FeedKey) is int places
            ? Math.Round(feed, places, MidpointRounding.AwayFromZero) == written
            : feed == written;
    }

    /// <summary>
    /// Tells whether the program has one feed after both steps: the same number, or the feed of the same word that
    /// set it from an expression, which NCX reads where the word stands (virtual machine 1, 3.6).
    /// </summary>
    public static bool SameFeed(HeidenhainBlock writing, int first, int second)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        ChannelSnapshot one = steps[first].After;
        ChannelSnapshot other = steps[second].After;
        bool unknown = one.Unknown.Contains(FeedKey);
        if (unknown != other.Unknown.Contains(FeedKey))
        {
            return false;
        }

        if (!unknown)
        {
            return one.Motion.Feed == other.Motion.Feed;
        }

        return SourceOf(writing, first) is int source && SourceOf(writing, second) == source;
    }

    /// <summary>
    /// Tells whether Klartext writes the feed with the motion of the block where it changes: a LINE and an ARC
    /// (heidenhain 8 rules 2 and 4); RAPID, HOME and M99 run at FMAX.
    /// </summary>
    public static bool IsFeedMotion(Block block)
    {
        return block.Verb?.Key is "LINE" or "ARC";
    }

    /// <summary>
    /// Keeps the F of a block without its motion for the next motion, where Klartext writes it: the control has not
    /// taken it over yet, so the next line or arc writes the feed where it differs (language 4.3; controllers
    /// heidenhain.md 2).
    /// </summary>
    public static void KeepPending(HeidenhainBlock writing)
    {
        writing.Take(FeedKey);
    }

    // A feed that the virtual machine keeps UNKNOWN comes from an expression (virtual machine 1), and a Q parameter may
    // stand wherever a number stands (controllers heidenhain.md 6), so it is F with the parameter, FQ1: the F of the
    // block or of the earlier block that set the feed, written where the control has another value active, and only
    // where the parameter still has the value NCX read at that word (HeidenhainParameters).
    private static string? ParameterFeed(HeidenhainBlock writing)
    {
        int index = writing.Step.Index;
        if (SourceOf(writing, index) is not int source)
        {
            writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
                "The feed of the program comes from an expression that no F word before this motion states, so "
                + "Klartext has no F for it (virtual machine 1, 3.10; controllers heidenhain.md 6).");
            return null;
        }

        Word word = writing.LookAhead.Steps[source].Block.Find(FeedKey)
            ?? throw new InvalidOperationException("The source of the feed has no F.");
        if (HeidenhainParameters.Of(word.Value) is not string parameter)
        {
            HeidenhainParameters.ReportValue(writing, word);
            return null;
        }

        // F of a motion stands after the VAR lines of its block (language 5 rule 3; HeidenhainCompiler.WriteBlock).
        if (HeidenhainParameters.Holds(writing, writing.Target.ActiveOf(FeedKey), parameter, afterVariables: true))
        {
            return null;
        }

        if (!HeidenhainParameters.Keeps(writing, word, parameter, source, afterVariables: true))
        {
            return null;
        }

        writing.Target.Set(FeedKey, HeidenhainParameters.Active(parameter, index));
        return FeedKey + parameter;
    }

    // The step of the F word that set a feed the virtual machine keeps UNKNOWN (HeidenhainParameters.SourceOf).
    private static int? SourceOf(HeidenhainBlock writing, int index)
    {
        return HeidenhainParameters.SourceOf(writing, index, FeedKey, block => block.Find(FeedKey));
    }

    // The Q parameter of the F word that set a feed the virtual machine keeps UNKNOWN; null where none sets it or it is
    // no parameter.
    private static string? ParameterOf(HeidenhainBlock writing, int index)
    {
        return SourceOf(writing, index) is int source
            && writing.LookAhead.Steps[source].Block.Find(FeedKey) is Word word
            ? HeidenhainParameters.Of(word.Value)
            : null;
    }

    // The number an F with a number wrote, F1,5 as 1.5 with the decimal separator of the machine (machine-config 2);
    // false for FQ1, a mark and an unknown value.
    private static bool TryNumberOf(HeidenhainBlock writing, string? active, out decimal feed)
    {
        feed = 0;
        if (active is null || !active.StartsWith(FeedKey, StringComparison.Ordinal))
        {
            return false;
        }

        string digits = active.Substring(FeedKey.Length)
            .Replace(writing.Numbers.DecimalSeparator, ".", StringComparison.Ordinal);
        return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out feed);
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
