using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The codes of the evaluator lie in its range VM900-VM949 (DiagnosticCodes.cs, D98), so that the codes of the block
/// execution written beside it cannot clash with them.
/// </summary>
public sealed class EvaluatorDiagnosticCodesTests
{
    [Theory]
    [InlineData(DiagnosticCodes.DivisionByZero)]
    [InlineData(DiagnosticCodes.UnassignedVariableRead)]
    [InlineData(DiagnosticCodes.StringWhereNumberIsRequired)]
    [InlineData(DiagnosticCodes.SystemVariableUnknown)]
    [InlineData(DiagnosticCodes.FunctionArgumentCount)]
    [InlineData(DiagnosticCodes.ResultUndefined)]
    [InlineData(DiagnosticCodes.ResultOutOfRange)]
    [InlineData(DiagnosticCodes.IndexNeedsSystemVariable)]
    [InlineData(DiagnosticCodes.IndexSelectsNoRegister)]
    public void EvaluationCode_Each_LiesInVm900ToVm949(string code)
    {
        Assert.Matches("^VM9[0-4][0-9]$", code);
    }
}
