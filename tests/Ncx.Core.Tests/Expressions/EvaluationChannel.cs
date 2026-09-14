using System.Globalization;
using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.State;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// One channel of the mill-turn test machine (StateMachines.MillTurn) whose variables a test sets by hand, as VAR would
/// set them, and the expressions of the test evaluated against them on one block of line 12.
/// </summary>
internal sealed class EvaluationChannel
{
    /// <summary>
    /// The line of the block the expressions stand in, which every diagnostic carries (D98).
    /// </summary>
    public const int BlockLine = 12;

    /// <summary>
    /// A channel at the start of a run, without variables.
    /// </summary>
    /// <param name="unassigned">[variables] unassigned, the evaluator's setting for an unassigned variable
    /// (D38).</param>
    public EvaluationChannel(UnassignedVariable unassigned = UnassignedVariable.Error)
    {
        Unassigned = unassigned;
    }

    public ChannelState State { get; } = new(StateMachines.MillTurn());

    public UnassignedVariable Unassigned { get; }

    /// <summary>
    /// The block the expressions stand in; a test replaces it with a generated block to see the originating line.
    /// </summary>
    public Block Block { get; init; } = new() { Line = BlockLine, Words = [] };

    public Diagnostics Diagnostics { get; } = new("test.ncx");

    /// <summary>
    /// A decimal from its text, for the data of a theory, which cannot hold a decimal.
    /// </summary>
    public static decimal DecimalOf(string text)
    {
        return decimal.Parse(
            text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Assigns a number, as VAR:name=number does: an integer when it is whole, a decimal otherwise.
    /// </summary>
    public void Set(string name, decimal number)
    {
        string text = number.ToString(CultureInfo.InvariantCulture);
        Value value = decimal.Truncate(number) == number
            ? new IntegerValue((long)number, text)
            : new DecimalValue(number, text);
        State.Vars.Set(name, VariableValue.Of(value));
    }

    /// <summary>
    /// Assigns a string, as VAR:name="TEXT" does.
    /// </summary>
    public void Set(string name, string text)
    {
        State.Vars.Set(name, VariableValue.Of(new StringValue(text)));
    }

    /// <summary>
    /// Parses an expression that must be valid and evaluates it on the block.
    /// </summary>
    public ExprResult? Evaluate(string expression)
    {
        ExprNode tree = ExprStructure.ParseValid(expression);
        return Evaluator.Evaluate(tree, State.Vars, Unassigned, Block, Diagnostics);
    }

    /// <summary>
    /// Evaluates an expression that must give a number and no diagnostic.
    /// </summary>
    public decimal NumberOf(string expression)
    {
        ExprResult? result = Evaluate(expression);

        Assert.True(Diagnostics.Items.Count == 0, Diagnostics.ToText());
        Assert.NotNull(result);
        Assert.True(result.Number.HasValue, $"{{{expression}}} gives {result}, not a number.");
        return result.Number.GetValueOrDefault();
    }

    /// <summary>
    /// Evaluates an expression that must fail: no value and exactly one ERROR, which it returns.
    /// </summary>
    public Diagnostic ErrorOf(string expression)
    {
        ExprResult? result = Evaluate(expression);

        Assert.Null(result);
        Diagnostic error = Assert.Single(Diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        return error;
    }
}
