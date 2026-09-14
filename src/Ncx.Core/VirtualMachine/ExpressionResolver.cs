using System.Globalization;
using Ncx.Core.Catalog;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// What INTERPRETED mode evaluates before a block executes (virtual machine 1, 3.6): the condition of IF, which decides
/// whether the block executes, and every other expression of the block, which becomes the number or the string it
/// gives. The state words then apply exactly as in STATIC mode, with the values STATIC mode leaves UNKNOWN resolved: an
/// axis word, VAR, ARG, TIMES or F from an expression (implementation 14, P4-01).
/// </summary>
internal static class ExpressionResolver
{
    // The condition of the JUMP or CALL in the same block (language 4.9).
    private const string ConditionKey = "IF";

    /// <summary>
    /// IF evaluates to 0 or not 0 (virtual machine 3.6), and its block executes when the expression is not 0 (language
    /// 4.9).
    /// </summary>
    /// <param name="block">The block, before it executes.</param>
    /// <param name="vars">The variables of the channel as the block finds them.</param>
    /// <param name="unassigned">[variables] unassigned of the run, VmOptions.Unassigned (D38).</param>
    /// <param name="diagnostics">Where an ERROR of the evaluation goes.</param>
    /// <returns>True for a block without IF or with a condition that is not 0; false for 0; null after an
    /// ERROR.</returns>
    public static bool? ConditionHolds(Block block, VariableStore vars, UnassignedVariable unassigned,
        Diagnostics diagnostics)
    {
        if (block.Find(ConditionKey) is not Word condition)
        {
            return true;
        }

        ExprResult? result = Evaluate(condition, vars, unassigned, block, diagnostics);
        if (result is null)
        {
            return null;
        }

        // A condition is a number, 0 or not 0; a string where a number is required is an ERROR (virtual machine 5).
        if (result.Content is string content)
        {
            diagnostics.Error(block, DiagnosticCodes.StringWhereNumberIsRequired,
                $"{condition.ToCanonical()} gives the string \"{content}\", and IF needs a number, 0 or not 0 "
                + "(language 4.9, virtual machine 3.6, 5).");
            return null;
        }

        return NumberOf(result, condition) != 0;
    }

    /// <summary>
    /// The block with every expression but that of its IF replaced by the value it gives, in the order the words
    /// stand. Every expression reads the variables as the block finds them, because the state words of a block do not
    /// depend on each other (virtual machine 3 step 3). IF keeps its expression as written, which the flow events carry
    /// as the condition (virtual machine 7).
    /// </summary>
    /// <param name="block">The block, before it executes.</param>
    /// <param name="vars">The variables of the channel as the block finds them.</param>
    /// <param name="unassigned">[variables] unassigned of the run, VmOptions.Unassigned (D38).</param>
    /// <param name="diagnostics">Where an ERROR of the evaluation goes.</param>
    /// <returns>The block as it executes; the block itself when it has no other expression; null after an
    /// ERROR.</returns>
    public static Block? Resolve(Block block, VariableStore vars, UnassignedVariable unassigned,
        Diagnostics diagnostics)
    {
        var words = new List<Word>(block.Words.Count);
        Word? verb = block.Verb;
        bool resolvedAny = false;
        foreach (Word word in block.Words)
        {
            Word resolved = word;
            if (word.Key != ConditionKey && word.Value is ExprValue)
            {
                if (ValueOf(word, block, vars, unassigned, diagnostics) is not Value value)
                {
                    return null;
                }

                resolved = word with { Value = value };
                resolvedAny = true;
            }

            words.Add(resolved);
            if (ReferenceEquals(word, block.Verb))
            {
                verb = resolved;
            }
        }

        return resolvedAny ? block with { Words = words, Verb = verb } : block;
    }

    /// <summary>
    /// The value of a number an expression computed, as NCX writes a number (language 3): an integer when the number is
    /// whole, so that a word that takes an integer takes it (TIMES={$Q1 + 1}), a decimal otherwise; the text as the
    /// events write numbers, invariant culture and no trailing zeros (EventText.Number).
    /// </summary>
    /// <param name="number">The number the expression gave.</param>
    public static Value NumberValueOf(decimal number)
    {
        string text = number == 0m
            ? "0"
            : number.ToString("0.############################", CultureInfo.InvariantCulture);
        if (IsWhole(number))
        {
            return new IntegerValue(decimal.ToInt64(number), text);
        }

        return new DecimalValue(number, text);
    }

    // An expression stands wherever a number is allowed (language 3) and gives the value of its word: a number, or a
    // string where the word takes one (VAR:QS2={$QS1}). A string where the word takes a number is the ERROR of virtual
    // machine 5, and so is a number with decimals where the word takes an integer (unknown value).
    private static Value? ValueOf(Word word, Block block, VariableStore vars, UnassignedVariable unassigned,
        Diagnostics diagnostics)
    {
        ExprResult? result = Evaluate(word, vars, unassigned, block, diagnostics);
        if (result is null)
        {
            return null;
        }

        ValueKinds kinds = ValueKindsOf(word, block);
        if (result.Content is string content)
        {
            if (kinds.HasFlag(ValueKinds.String))
            {
                return new StringValue(content);
            }

            diagnostics.Error(block, DiagnosticCodes.StringWhereNumberIsRequired,
                $"{word.ToCanonical()} gives the string \"{content}\", and {KeyOf(word)} takes a number (virtual "
                + "machine 5).");
            return null;
        }

        Value value = NumberValueOf(NumberOf(result, word));
        if (value is DecimalValue fraction && !kinds.HasFlag(ValueKinds.Decimal))
        {
            diagnostics.Error(block, DiagnosticCodes.ValueNotAnInteger,
                $"{word.ToCanonical()} gives {fraction.Text}, and {KeyOf(word)} takes an integer (language 3, 4; "
                + "virtual machine 5).");
            return null;
        }

        return value;
    }

    // The parser built the tree of every expression of a program that runs: a program with an expression it could
    // not read has an ERROR and does not run (virtual machine 2.9).
    private static ExprResult? Evaluate(Word word, VariableStore vars, UnassignedVariable unassigned, Block block,
        Diagnostics diagnostics)
    {
        if (word.Value is not ExprValue { Tree: ExprNode tree })
        {
            throw new InvalidOperationException(
                $"{word.ToCanonical()} has no parsed expression; the parser reads every expression of a program that "
                + "runs.");
        }

        return Evaluator.Evaluate(tree, vars, unassigned, block, diagnostics);
    }

    // INTERPRETED mode holds no UNKNOWN: every VAR and ARG it executes is resolved, and a SYS_ name it cannot read is
    // the ERROR of the evaluator (virtual machine 3.6). UNKNOWN comes only from a variable STATIC mode set.
    private static decimal NumberOf(ExprResult result, Word word)
    {
        return result.Number ?? throw new InvalidOperationException(
            $"{word.ToCanonical()} is UNKNOWN, which INTERPRETED mode never holds (virtual machine 1, 3.6).");
    }

    // The value types a word takes in its block (WordCheck); a machine axis word and a native parameter of a
    // CYCLE:controller=n block take a number (D93, D94).
    private static ValueKinds ValueKindsOf(Word word, Block block)
    {
        return word.Definition is WordDefinition definition
            ? WordCheck.ValueKindsOf(word, definition, block)
            : ValueKinds.Number;
    }

    // A whole number within the integers of language 3, as the model keeps them (IntegerValue).
    private static bool IsWhole(decimal number)
    {
        return decimal.Truncate(number) == number && number >= long.MinValue && number <= long.MaxValue;
    }

    // TIMES, VAR:Q1: the key with its address, as a message names the word.
    private static string KeyOf(Word word)
    {
        return word.Addr is null ? word.Key : word.Key + ":" + word.Addr;
    }
}
