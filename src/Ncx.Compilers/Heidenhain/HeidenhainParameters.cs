using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// A feed or a speed that a Q parameter sets. A Q parameter may stand wherever a number stands (controllers
/// heidenhain.md 6), so F and S take it in place of their number, FQ1 and SQ2. NCX reads the parameter where its word
/// stands, before the words of its block act (virtual machine 3.6), and the control reads it where Klartext writes the
/// F or the S, which for the F of a block without a motion is the next motion, and for the RPM of the block after a
/// TOOL the TOOL CALL (heidenhain 8 rules 2 and 3); the virtual machine keeps the value UNKNOWN in STATIC mode (virtual
/// machine 1). The parameter is written only where it has the value NCX read, and the control keeps the value it read
/// until the next F or S, so what the control has active is the parameter with the step that read it, Q1@12.
/// </summary>
internal static class HeidenhainParameters
{
    // Between the parameter and the step that read it in what the target state keeps: Q1@12.
    private const string Place = "@";

    /// <summary>
    /// The Q parameter a feed or a speed takes in place of its number, Q1 of {$Q1}; null for any other value, a number,
    /// a formula or a parameter with a sign.
    /// </summary>
    public static string? Of(Value value)
    {
        return value is ExprValue { Tree: ExprNode tree } && HeidenhainFormula.SignedParameter(tree) is string signed
            && signed.StartsWith('+')
            ? signed.Substring(1)
            : null;
    }

    /// <summary>
    /// The step whose word set a variable that the virtual machine keeps UNKNOWN at a step: that step or the last one
    /// before it in walk order with such a word, while the variable stays unknown, in the program of the step; null
    /// where none sets it with a word, or a @RESTORE brought an earlier value back (virtual machine 3.10).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="index">The step at which the variable is unknown.</param>
    /// <param name="stateKey">The state key of the variable: F, RPM:S1.</param>
    /// <param name="wordOf">The word of a block that sets the variable; null for a block without one.</param>
    public static int? SourceOf(HeidenhainBlock writing, int index, string stateKey, Func<Block, Word?> wordOf)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        for (int at = index; at >= 0 && steps[at].After.Unknown.Contains(stateKey); at--)
        {
            Block block = steps[at].Block;
            if (wordOf(block) is not null)
            {
                return at;
            }

            if (block.Has("@RESTORE") || block.Has("PROGRAM", null, "BEGIN"))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// What the control has active once the step being written reads the parameter: Q1@12.
    /// </summary>
    public static string Active(string parameter, int index)
    {
        return parameter + Place + index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tells whether the control has the value of the parameter active already: an earlier F or S read the parameter,
    /// and it has kept its value up to the F or S of the block being written.
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="active">What the target state keeps under the F or the speed of the spindle.</param>
    /// <param name="parameter">The parameter the block would write: Q1.</param>
    /// <param name="afterVariables">True where the F or S stands after the VAR lines of its block, the F of a motion;
    /// false for the S of TOOL CALL and of the RPM template, which stand before them.</param>
    public static bool Holds(HeidenhainBlock writing, string? active, string parameter, bool afterVariables)
    {
        return Holds(writing, active, parameter, afterVariables, writing.Step.Index);
    }

    /// <summary>
    /// Tells whether the control has the value of the parameter active up to a step: an earlier F or S read the
    /// parameter, and it has kept its value up to the F or S of that step.
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="active">What the target state keeps under the F or the speed of the spindle.</param>
    /// <param name="parameter">The parameter: Q1.</param>
    /// <param name="afterVariables">True where the F or S stands after the VAR lines of its block.</param>
    /// <param name="to">The step up to which the parameter keeps its value.</param>
    public static bool Holds(HeidenhainBlock writing, string? active, string parameter, bool afterVariables, int to)
    {
        string[] parts = active?.Split(Place) ?? [];
        if (parts.Length != 2 || parts[0] != parameter
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int read))
        {
            return false;
        }

        return KeepsValue(writing, parameter, read, afterVariables, to, afterVariables, labels: true);
    }

    /// <summary>
    /// Tells whether the parameter has, where the block being written reads it, the value NCX read at its word; reports
    /// it where it may not (CMP117).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="word">The word that sets the feed or the speed with the parameter.</param>
    /// <param name="parameter">The parameter: Q1.</param>
    /// <param name="source">The step of the word: the step being written, an earlier one, or the block after a TOOL
    /// whose RPM the TOOL CALL takes.</param>
    /// <param name="afterVariables">True where the F or S stands after the VAR lines of its block.</param>
    public static bool Keeps(HeidenhainBlock writing, Word word, string parameter, int source, bool afterVariables)
    {
        // The TOOL CALL reads the RPM of the block after the TOOL before that block, and before the VAR lines of its
        // own block, which NCX runs first. A jump to a label on that block does not pass the TOOL CALL, and the label
        // writes the speed again there (HeidenhainFlow.WriteLabel).
        int index = writing.Step.Index;
        bool keeps = source <= index
            ? KeepsValue(writing, parameter, source, false, index, afterVariables, labels: true)
            : KeepsValue(writing, parameter, index, afterVariables, source, false, labels: false);
        if (!keeps)
        {
            writing.Error(DiagnosticCodes.HeidenhainParameterReadElsewhere,
                $"{word.ToCanonical()} on line {writing.LookAhead.Steps[source].Block.Line}: Klartext writes it here "
                + $"with {parameter} in place of the number (controllers heidenhain.md 6), and {parameter} may have "
                + "another value here than where NCX reads it, since an assignment of it, a label that a jump reaches, "
                + "RAW or a call of another file stands between; nothing is written for it (virtual machine 3.6; "
                + "language 4.9).");
        }

        return keeps;
    }

    /// <summary>
    /// Reports a feed or a speed whose value has no Klartext form (CMP102).
    /// </summary>
    // TODO(question): heidenhain.md 6 lets a Q parameter stand where a number stands and gives no form for a formula
    // there, and heidenhain 2 and 4 write F and S without a sign, F500, S1592; a formula or a parameter with a minus
    // sign in F or S is reported (CMP102), as a formula in a coordinate is (HeidenhainNumbers).
    public static void ReportValue(HeidenhainBlock writing, Word word)
    {
        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"{word.ToCanonical()}: F and S take a number or a Q parameter in place of it, F500 or FQ1 (controllers "
            + "heidenhain.md 2, 4, 6), and the documents give no form for a formula or a sign there; nothing is "
            + "written for it.");
    }

    // The parameter has one value from a point of one step to a point of the same or a later step in walk order: no
    // VAR or ARG of it between the two points, where the lines of a block that assign a Q parameter stand before its
    // motion and after its TOOL CALL (HeidenhainCompiler.WriteBlock), no RAW or call of another file, which may assign
    // any parameter, and, where labels count, no label after the first point, which a jump reaches with the value its
    // own way left (language 4.9).
    private static bool KeepsValue(HeidenhainBlock writing, string parameter, int from, bool fromAfterVariables, int to,
        bool toAfterVariables, bool labels)
    {
        IReadOnlyList<BlockStep> steps = writing.LookAhead.Steps;
        if (from > to)
        {
            return false;
        }

        for (int at = from; at <= to; at++)
        {
            Block block = steps[at].Block;
            if (block.Has("RAW") || CallsAnotherFile(steps[at], block) || (labels && at > from && block.Has("LABEL")))
            {
                return false;
            }

            bool afterFirst = at > from || !fromAfterVariables;
            bool beforeSecond = at < to || toAfterVariables;
            if (afterFirst && beforeSecond && Assigns(block, parameter))
            {
                return false;
            }
        }

        return true;
    }

    // VAR:Q1 assigns the parameter, and so does ARG:Q1, which Klartext sets before the call (controller-mapping 6, CALL
    // + ARG).
    private static bool Assigns(Block block, string parameter)
    {
        foreach (Word word in block.Words)
        {
            if (word.Key is "VAR" or "ARG" && word.Addr == parameter)
            {
                return true;
            }
        }

        return false;
    }

    // CALL="name" of a program outside the file, CALL PGM name (language 4.9, CALL; HeidenhainSubprograms).
    private static bool CallsAnotherFile(BlockStep step, Block block)
    {
        return block.Find("CALL")?.Value is StringValue name && !step.After.Flow.Subs.ContainsKey(name.Content);
    }
}
