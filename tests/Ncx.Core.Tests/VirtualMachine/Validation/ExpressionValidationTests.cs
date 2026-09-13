using Ncx.Core.Model;
using Ncx.Core.Tests.Expressions;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The expression rules of virtual machine 5, one test per rule with the smallest input: the assignment of a SYS_
/// variable, the unresolved expressions of STATIC mode (VM 1), and the ERRORs of evaluation, which the evaluator of
/// INTERPRETED mode raises (language 4.12, D38).
/// </summary>
public sealed class ExpressionValidationTests
{
    // VM 2.7, 5: an assignment to a SYS_ variable is an ERROR.
    [Fact]
    public void Assignment_ToASystemVariable_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("VAR:SYS_TOOL=1");

        RuleAssert.Only(vm, DiagnosticCodes.SystemVariableAssigned);
    }

    // VM 1, 5: an unresolved expression in STATIC mode is a WARNING, on the block of the first.
    [Fact]
    public void UnresolvedExpression_InStaticMode_Warns()
    {
        string text = VmHarness.File("VAR:Q1={1 + 2}", "PROGRAM=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()),
            DiagnosticCodes.UnresolvedExpressions);

        Assert.Equal(3, warning.Line);
        Assert.StartsWith("An expression is not evaluated in STATIC mode", warning.Message, StringComparison.Ordinal);
    }

    // VM 5: the unresolved expressions are counted and reported once; an expression of a subprogram walked twice
    // counts once, as each is one expression of the file.
    [Fact]
    public void UnresolvedExpressions_Several_AreCountedAndReportedOnce()
    {
        string text = VmHarness.File(
            "VAR:Q1={1 + 2}", "VAR:Q2={$Q1 * 2}", "CALL=9 TIMES=2", "PROGRAM=END", "SUB=BEGIN NAME=9",
            "VAR:Q3={$Q2 - 1}", "SUB=END");

        Diagnostic warning = RuleAssert.Only(VmHarness.Run(text, VmMachines.Default()),
            DiagnosticCodes.UnresolvedExpressions);

        Assert.StartsWith("3 expressions are not evaluated in STATIC mode, the first in this block", warning.Message,
            StringComparison.Ordinal);
    }

    // Language 4.12, VM 5: a division by zero is an ERROR of evaluation.
    [Fact]
    public void DivisionByZero_IsAnError()
    {
        var channel = new EvaluationChannel();
        channel.ErrorOf("1 / 0");

        RuleAssert.Only(channel.Diagnostics, DiagnosticCodes.DivisionByZero);
    }

    // VM 3.6, 5, D38: reading an unassigned variable is an ERROR of evaluation.
    [Fact]
    public void UnassignedVariable_Read_IsAnError()
    {
        var channel = new EvaluationChannel();
        channel.ErrorOf("$Q9");

        RuleAssert.Only(channel.Diagnostics, DiagnosticCodes.UnassignedVariableRead);
    }

    // VM 5: a string where a number is required is an ERROR of evaluation.
    [Fact]
    public void String_WhereANumberIsRequired_IsAnError()
    {
        var channel = new EvaluationChannel();
        channel.Set("QS1", "TEXT");
        channel.ErrorOf("$QS1 + 1");

        RuleAssert.Only(channel.Diagnostics, DiagnosticCodes.StringWhereNumberIsRequired);
    }
}
