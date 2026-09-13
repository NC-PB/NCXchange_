using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The rule tests of virtual machine 5: a file or a few blocks, the smallest input that breaks one rule, and the one
/// diagnostic it raises, with the code and the severity of the table of the validation (DiagnosticTable).
/// </summary>
internal static class RuleAssert
{
    /// <summary>
    /// Parses a whole file as a user file.
    /// </summary>
    public static NcxProgram ParseFile(string text)
    {
        return Parser.Parse(text, "test.ncx", new ParserOptions());
    }

    /// <summary>
    /// Parses a file of one program with these blocks between PROGRAM=BEGIN, line 2, and PROGRAM=END.
    /// </summary>
    public static NcxProgram ParseProgram(params string[] lines)
    {
        return ParseFile("FILE=BEGIN NCX=1\nPROGRAM=BEGIN\n" + string.Join("\n", lines) + "\nPROGRAM=END\nFILE=END\n");
    }

    /// <summary>
    /// The one diagnostic of the list: this code, with the severity the table of the validation gives it; the test
    /// fails with the whole list otherwise.
    /// </summary>
    public static Diagnostic Only(Diagnostics diagnostics, string code)
    {
        Assert.True(diagnostics.Items.Count == 1, $"Expected {code} alone, got:\n{diagnostics.ToText()}");
        Diagnostic diagnostic = diagnostics.Items[0];
        Assert.Equal(code, diagnostic.Code);
        ValidationRule? rule = DiagnosticTable.Find(code);
        Assert.NotNull(rule);
        Assert.Equal(rule.Severity, diagnostic.Severity);
        return diagnostic;
    }

    /// <summary>
    /// The one diagnostic of a virtual machine of the tests.
    /// </summary>
    public static Diagnostic Only(VmHarness vm, string code)
    {
        return Only(vm.Diagnostics, code);
    }
}
