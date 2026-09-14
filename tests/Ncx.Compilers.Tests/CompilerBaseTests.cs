using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The skeleton of CompilerBase through the fake compiler: the number format of [format], RAW and the native cycles of
/// another controller (D94), and the modal words of the target state (phase 3, P3-03 "Done when").
/// </summary>
public sealed class CompilerBaseTests
{
    // The ERROR text of RAW and of a CYCLE:controller=n block of another controller, the same for both (language 4.1,
    // 4.7.1; controller-mapping 9; D5, D94).
    private const string AnErrorAtCompileTime =
        ": an ERROR at compile time (language 4.1, 4.7.1; controller-mapping 9; D5, D94).";

    // Three blocks that move, with a decimal on X, a negative Y, a feed and a whole Z.
    private static readonly string s_threeBlocks = FakeCompile.Program(
        "LINE X=10.5 Y=-2.25 F=250",
        "LINE X=12",
        "RAPID Z=5");

    // Machine-config 2: the point, three decimals on the axes and one on F without trailing zeros, the decimal point on
    // every real number of the fake's Fanuc form, the blocks numbered from N10 in steps of 10, LF.
    [Fact]
    public void Format_PointTableWithBlockNumbersFromTen_WritesThreeBlocksWithItsNumbers()
    {
        CompileResult result = FakeCompile.Run(s_threeBlocks, FakeMachines.Mill(format: FakeMachines.PointFormat));

        Assert.Equal(
            "%\nO1 (T)\nN10 G1 X10.5 Y-2.25 F250.\nN20 X12.\nN30 G0 Z5.\nN40 M30\n%\n",
            FakeCompile.TextOf(result));
    }

    // Machine-config 2: the comma, four decimals on the axes and none on F with trailing zeros, the blocks numbered
    // from N1 in steps of 1, CRLF; the same three blocks.
    [Fact]
    public void Format_CommaTableWithTrailingZeros_WritesTheSameBlocksWithItsNumbers()
    {
        CompileResult result = FakeCompile.Run(s_threeBlocks, FakeMachines.Mill(format: FakeMachines.CommaFormat));

        Assert.Equal(
            "%\r\nO1 (T)\r\nN1 G1 X10,5000 Y-2,2500 F250\r\nN2 X12,0000\r\nN3 G0 Z5,0000\r\nN4 M30\r\n%\r\n",
            FakeCompile.TextOf(result));
    }

    // Phase 3, P3-03: a modal word is written only on change; G1 and the feed stand once while they stay active.
    [Fact]
    public void TargetState_ModalWordThatStaysActive_IsWrittenOnce()
    {
        string program = FakeCompile.Program("LINE X=1 F=100", "LINE X=2", "LINE X=3 F=100", "LINE X=4 F=120");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill()));

        Assert.Equal("%\nO1 (T)\nN10 G1 X1. F100.\nN20 X2.\nN30 X3.\nN40 X4. F120.\nN50 M30\n%\n", text);
    }

    // Language 4.1, D5: RAW:FANUC compiles to Fanuc, its source text written as it is.
    [Fact]
    public void Raw_OfTheSameController_IsWrittenVerbatim()
    {
        string program = FakeCompile.Program("RAW:FANUC=\"G10 L2 P1 Z-175.\"");

        string text = FakeCompile.TextOf(FakeCompile.Run(program, FakeMachines.Mill()));

        Assert.Equal("%\nO1 (T)\nN10 G10 L2 P1 Z-175.\nN20 M30\n%\n", text);
    }

    // Language 4.1, controller-mapping 9, D5: RAW:FANUC compiled for Heidenhain is an ERROR at compile time, and no
    // file is written.
    [Fact]
    public void Raw_OfAnotherController_IsTheErrorCmp001AndWritesNoFile()
    {
        string program = FakeCompile.Program("RAW:FANUC=\"G10 L2 P1 Z-175.\"");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill("heidenhain"), Controller.Heidenhain);

        Assert.Empty(result.Files);
        Diagnostic error = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.NativeTextOfAnotherController, error.Code);
        Assert.Equal(4, error.Line);
        Assert.Equal(
            "RAW:FANUC compiles only to FANUC, the controller or builder it names, and the machine \"Fake mill\" is a "
            + "Heidenhain machine" + AnErrorAtCompileTime,
            error.Message);
    }

    // Language 4.1, machine-config 1: RAW of the builder compiles to a machine of that builder, and to no other.
    [Fact]
    public void Raw_OfTheBuilder_CompilesOnlyToAMachineOfThatBuilder()
    {
        string program = FakeCompile.Program("RAW:NAKAMURA=\"G411 C1. I9090\"");
        string nakamura = FakeMachines.Mill().Replace(
            "controller = \"fanuc\"", "controller = \"fanuc\"\nbuilder = \"nakamura\"", StringComparison.Ordinal);

        string text = FakeCompile.TextOf(FakeCompile.Run(program, nakamura));
        CompileResult other = FakeCompile.Run(program, FakeMachines.Mill());

        Assert.Equal("%\nO1 (T)\nN10 G411 C1. I9090\nN20 M30\n%\n", text);
        Assert.Equal([DiagnosticCodes.NativeTextOfAnotherController], FakeCompile.Codes(other).FindAll(IsCompiler));
    }

    // Language 4.7.1, D94: CYCLE:HEIDENHAIN=251 compiles to Heidenhain with the native parameters as written.
    [Fact]
    public void NativeCycle_OfTheSameFamily_IsWrittenWithItsParameters()
    {
        string program = FakeCompile.Program("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill("heidenhain"), Controller.Heidenhain);

        Assert.Equal("%\nO1 (T)\nN10 CYCL DEF 251 Q215=0 Q218=60\nN20 M30\n%\n", FakeCompile.TextOf(result));
    }

    // Language 4.7.1, D94: the native parameters are kept in source order after the cycle words.
    [Fact]
    public void NativeCycle_ParametersInAnotherOrder_KeepTheirSourceOrder()
    {
        string program = FakeCompile.Program("CYCLE:HEIDENHAIN=251 Q218=60 Q215=0");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill("heidenhain"), Controller.Heidenhain);

        Assert.Contains("\nN10 CYCL DEF 251 Q218=60 Q215=0\n", FakeCompile.TextOf(result), StringComparison.Ordinal);
    }

    // Language 4.7.1, controller-mapping 9, D94: the same cycle compiled for Fanuc is the same ERROR as RAW.
    [Fact]
    public void NativeCycle_OfAnotherFamily_IsTheSameErrorAsRaw()
    {
        string program = FakeCompile.Program("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill());

        Assert.Empty(result.Files);
        Diagnostic error = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.NativeTextOfAnotherController, error.Code);
        Assert.Equal(
            "CYCLE:HEIDENHAIN=251 with its native parameters compiles only to HEIDENHAIN, the controller family it "
            + "names, and the machine \"Fake mill\" is a Fanuc machine" + AnErrorAtCompileTime,
            error.Message);
    }

    // Controller-mapping 9: every block of another controller is reported before anything is written.
    [Fact]
    public void NativeText_TwoBlocksOfAnotherController_AreBothReported()
    {
        string program = FakeCompile.Program("RAW:SIEMENS=\"MSG(\\\"A\\\")\"", "CYCLE:HEIDENHAIN=200 Q200=2");

        CompileResult result = FakeCompile.Run(program, FakeMachines.Mill());

        Assert.Equal(2, FakeCompile.CompilerDiagnostics(result).Count);
        Assert.Empty(result.Files);
    }

    // Machine-config 2, D49: PROGRAM=END writes program_end, and a machine file without it cannot write the end.
    [Fact]
    public void ProgramEnd_WithoutProgramEndInFormat_IsTheErrorCmp010()
    {
        string machine = FakeMachines.Mill().Replace("program_end = \"M30\"\n", "", StringComparison.Ordinal);

        CompileResult result = FakeCompile.Run(FakeCompile.Program(), machine);

        Assert.Empty(result.Files);
        Diagnostic error = Assert.Single(FakeCompile.CompilerDiagnostics(result));
        Assert.Equal(DiagnosticCodes.TemplateMissing, error.Code);
        Assert.StartsWith("The machine \"Fake mill\" has no [format] program_end", error.Message,
            StringComparison.Ordinal);
    }

    // Virtual machine 2.9, code-guidelines 6: an ERROR of the parser stops the compile before its first block.
    [Fact]
    public void Compile_ErrorOfTheParser_WritesNoFile()
    {
        CompileResult result = FakeCompile.Run(FakeCompile.Program("LINE X=1 NOSUCHWORD=2"), FakeMachines.Mill());

        Assert.Empty(result.Files);
        Assert.True(result.Diagnostics.HasErrors);
        Assert.Empty(FakeCompile.CompilerDiagnostics(result));
    }

    // Machine-config 10: the file of one_file is named after the NCX file, with the extension of the controller.
    [Fact]
    public void Files_OneFile_IsNamedAfterTheNcxFile()
    {
        CompileResult result = FakeCompile.Run(FakeCompile.Program(), FakeMachines.Mill());

        Assert.Equal("T.nc", Assert.Single(result.Files).Name);
    }

    private static bool IsCompiler(string code)
    {
        return code.StartsWith("CMP", StringComparison.Ordinal);
    }
}
