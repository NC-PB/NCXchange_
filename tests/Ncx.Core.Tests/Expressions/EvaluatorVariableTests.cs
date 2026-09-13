using Ncx.Core.Expressions;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The variables an expression reads through the variable store of the channel (language 4.9, 4.12; virtual machine
/// 2.7, 3.6): assigned and unassigned variables, the locals V1 to V33 of the call that runs, strings, UNKNOWN, the
/// SYS_ names with their index, and where the ERROR is reported.
/// </summary>
public sealed class EvaluatorVariableTests
{
    // Language 4.9: $Q1 reads the variable VAR assigned.
    [Fact]
    public void Variable_Assigned_ReadsItsValue()
    {
        var channel = new EvaluationChannel();
        channel.Set("Q1", 10m);
        channel.Set("Q2", 2.5m);

        Assert.Equal(12.5m, channel.NumberOf("$Q1 + $Q2"));
    }

    // Language 4.9, D33: the Fanuc #105 is V105 and reads like any other variable.
    [Fact]
    public void Variable_FanucName_ReadsItsValue()
    {
        var channel = new EvaluationChannel();
        channel.Set("V105", 3m);

        Assert.Equal(6m, channel.NumberOf("$V105 * 2"));
    }

    // Virtual machine 3.6, D38: reading an unassigned variable is an ERROR, on the line of the block; the P4-01 case
    // {$Q9}.
    [Fact]
    public void Variable_Unassigned_IsAnError()
    {
        Diagnostic error = new EvaluationChannel().ErrorOf("$Q9");

        Assert.Equal(DiagnosticCodes.UnassignedVariableRead, error.Code);
        Assert.Equal(EvaluationChannel.BlockLine, error.Line);
        Assert.Contains("$Q9", error.Message, StringComparison.Ordinal);
    }

    // Virtual machine 3.6, D38: under unassigned = 0 an unassigned variable reads 0; the P4-01 case {$Q9} under the
    // option.
    [Fact]
    public void Variable_UnassignedUnderUnassignedZero_ReadsZero()
    {
        var channel = new EvaluationChannel(UnassignedVariable.Zero);

        Assert.Equal(0m, channel.NumberOf("$Q9"));
        Assert.Equal(1m, channel.NumberOf("$Q9 + 1"));
    }

    // Virtual machine 3.6: a CALL pushes the locals V1 to V33, and inside the call $V1 reads the callee's own.
    [Fact]
    public void Variable_LocalInsideACall_ReadsTheLocalOfTheCallee()
    {
        var channel = new EvaluationChannel();
        channel.Set("V1", 7m);
        channel.State.Vars.PushLocals();
        channel.Set("V1", 1m);

        Assert.Equal(1m, channel.NumberOf("$V1"));
    }

    // Virtual machine 3.6: the callee does not see the caller's locals, so the caller's V1 is unassigned in the call.
    [Fact]
    public void Variable_LocalOfTheCallerInsideACall_IsUnassigned()
    {
        var channel = new EvaluationChannel();
        channel.Set("V1", 7m);
        channel.State.Vars.PushLocals();

        Assert.Equal(DiagnosticCodes.UnassignedVariableRead, channel.ErrorOf("$V1").Code);
    }

    // Virtual machine 3.6, 4: after SUB=END or RETURN $V1 reads the caller's local again, and a variable the call set
    // outside V1 to V33 stays for the program.
    [Fact]
    public void Variable_AfterTheReturn_ReadsTheCallerLocalAndTheProgramVariable()
    {
        var channel = new EvaluationChannel();
        channel.Set("V1", 7m);
        channel.State.Vars.PushLocals();
        channel.Set("V1", 1m);
        channel.Set("Q1", 3m);
        channel.State.Vars.PopLocals();

        Assert.Equal(7m, channel.NumberOf("$V1"));
        Assert.Equal(3m, channel.NumberOf("$Q1"));
    }

    // Language 4.9: a variable may hold a string, and an expression that is only the variable gives the string.
    [Theory]
    [InlineData("$QS1")]
    [InlineData("($QS1)")]
    public void Variable_HoldingAString_GivesTheString(string expression)
    {
        var channel = new EvaluationChannel();
        channel.Set("QS1", "TEXT");

        ExprResult? result = channel.Evaluate(expression);

        Assert.Empty(channel.Diagnostics.Items);
        Assert.Equal(ExprResult.Of("TEXT"), result);
    }

    // Virtual machine 5: a string where a number is required is an ERROR: an operand, a comparison (the reading of the
    // open question in Evaluator.EvaluateBinary), a function argument, an index.
    [Theory]
    [InlineData("$QS1 + 1")]
    [InlineData("-$QS1")]
    [InlineData("NOT $QS1")]
    [InlineData("$QS1 == 1")]
    [InlineData("SIN($QS1)")]
    [InlineData("$SYS_TOOL[$QS1]")]
    public void Variable_StringWhereANumberIsRequired_IsAnError(string expression)
    {
        var channel = new EvaluationChannel();
        channel.Set("QS1", "TEXT");

        Diagnostic error = channel.ErrorOf(expression);

        Assert.Equal(DiagnosticCodes.StringWhereNumberIsRequired, error.Code);
        Assert.Contains("\"TEXT\"", error.Message, StringComparison.Ordinal);
    }

    // Virtual machine 1: a variable that holds UNKNOWN makes the value of the expression UNKNOWN.
    [Theory]
    [InlineData("$Q1")]
    [InlineData("$Q1 + 1")]
    [InlineData("$Q1 < 2")]
    [InlineData("SIN($Q1)")]
    public void Variable_HoldingUnknown_MakesTheValueUnknown(string expression)
    {
        var channel = new EvaluationChannel();
        channel.State.Vars.Set("Q1", VariableValue.Unknown);

        ExprResult? result = channel.Evaluate(expression);

        Assert.Empty(channel.Diagnostics.Items);
        Assert.NotNull(result);
        Assert.True(result.IsUnknown);
    }

    // Language 4.12, virtual machine 2.7: $SYS_TOOL reads the tool in the spindle of the holder called last.
    [Fact]
    public void SystemVariable_ActiveTool_ReadsTheToolInTheSpindle()
    {
        var channel = new EvaluationChannel();
        channel.State.Holders["H2"].SpindleTool = new ToolRef(12);
        channel.State.LastHolder = "H2";

        Assert.Equal(12m, channel.NumberOf("$SYS_TOOL"));
    }

    // Virtual machine 2.7, 3.6: a SYS_ name the configuration does not map (SYS_MPOS_B), maps to a value the control
    // cannot read (SYS_PART_MAIN), or whose state the virtual machine does not hold (register 99 of the wear table) is
    // an ERROR in INTERPRETED mode.
    [Theory]
    [InlineData("$SYS_MPOS_B")]
    [InlineData("$SYS_PART_MAIN")]
    [InlineData("$SYS_WEAR_Z[99]")]
    public void SystemVariable_NotKnown_IsAnErrorInInterpretedMode(string expression)
    {
        Diagnostic error = new EvaluationChannel().ErrorOf(expression);

        Assert.Equal(DiagnosticCodes.SystemVariableUnknown, error.Code);
        Assert.Contains(expression, error.Message, StringComparison.Ordinal);
    }

    // Language 4.12: the index of a SYS_ name may itself be an expression, and it is evaluated before the register is
    // read.
    [Theory]
    [InlineData("$SYS_WEAR_Z[1 / 0]", DiagnosticCodes.DivisionByZero)]
    [InlineData("$SYS_WEAR_Z[$Q9 + 1]", DiagnosticCodes.UnassignedVariableRead)]
    public void SystemVariable_IndexExpression_IsEvaluatedFirst(string expression, string code)
    {
        Assert.Equal(code, new EvaluationChannel().ErrorOf(expression).Code);
    }

    // Language 4.12: the index selects a register or table row, a whole number (the reading of the open question in
    // Evaluator.RegisterOf).
    [Theory]
    [InlineData("$SYS_TOOL[99.5]")]
    [InlineData("$SYS_TOOL[10000000000]")]
    public void SystemVariable_IndexNotAWholeRegisterNumber_SelectsNoRegister(string expression)
    {
        Assert.Equal(DiagnosticCodes.IndexSelectsNoRegister, new EvaluationChannel().ErrorOf(expression).Code);
    }

    // Language 4.12, D51: the index selects a register or table row of a SYS_ name; on another variable it is an ERROR
    // (the reading of the open question in Evaluator.EvaluateVariable).
    [Fact]
    public void Variable_IndexOnAVariableThatIsNotASystemName_IsAnError()
    {
        var channel = new EvaluationChannel();
        channel.Set("Q1", 10m);

        Assert.Equal(DiagnosticCodes.IndexNeedsSystemVariable, channel.ErrorOf("$Q1[2]").Code);
    }

    // D98: an ERROR on a generated block carries the line of the block it was generated for.
    [Fact]
    public void Error_OnAGeneratedBlock_CarriesTheLineOfTheOriginatingBlock()
    {
        var channel = new EvaluationChannel
        {
            Block = new Block { Line = 40, Words = [], IsGenerated = true, OriginLine = 12 },
        };

        Diagnostic error = channel.ErrorOf("$Q9");

        Assert.Equal(40, error.Line);
        Assert.Equal(12, error.OriginLine);
    }

    // The evaluator stops at the first problem in reading order, so an expression gives at most one ERROR.
    [Fact]
    public void Evaluate_TwoProblems_ReportsTheFirstOnly()
    {
        Assert.Equal(DiagnosticCodes.UnassignedVariableRead, new EvaluationChannel().ErrorOf("$Q9 + 1 / 0").Code);
    }
}
