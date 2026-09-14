using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// CALL of an external program in INTERPRETED mode (language 4.9; virtual machine 2.9, 3.6): loaded by its file name
/// from the working directory, which the tests stand in for with texts, run with the caller's state in its own file,
/// with its own PROGRAM frame, not contradicting the caller's UNITS and WORKPLANE.
/// </summary>
public sealed class ExternalProgramTests
{
    // The caller: X at 0, the external program O9010, then one more millimetre.
    private static readonly string s_caller = VmHarness.File(
        "UNITS=MM", "RAPID X=0 Y=0 Z=0", "CALL=\"O9010\"", "RAPID IX=1", "PROGRAM=END");

    // VM 3.6: the external program runs with the caller's state and the flow returns after the CALL; the caller stays
    // the program that runs.
    [Fact]
    public void CallExternal_RunsWithTheCallersStateAndReturnsAfterTheCall()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("RAPID IX=10"))
            .Run(s_caller);

        vm.AssertNoDiagnostics();
        Assert.Equal(11m, vm.Position("X").Value);
        Assert.Equal("TEST", vm.State.Program.Name);
        Assert.Empty(vm.State.Flow.Calls);
        Assert.Single(vm.Events.LinesOf("PROGRAM_END"));
    }

    // Language 4.9, VM 3.6: CALL with TIMES=n runs the external program n times.
    [Fact]
    public void CallExternal_WithTimes_RunsTheProgramTimesTimes()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("RAPID IX=10"))
            .Run(VmHarness.File("UNITS=MM", "RAPID X=0 Y=0 Z=0", "CALL=\"O9010\" TIMES=2", "PROGRAM=END"));

        vm.AssertNoDiagnostics();
        Assert.Equal(20m, vm.Position("X").Value);
    }

    // VM 3.6, 5: an external program the working directory does not hold is a missing call target: ERROR.
    [Fact]
    public void CallExternal_ProgramTheWorkingDirectoryDoesNotHold_IsAnError()
    {
        InterpretedHarness vm = new InterpretedHarness().Run(s_caller);

        Diagnostic error = vm.Single(DiagnosticCodes.ExternalProgramNotFound);
        Assert.Contains("O9010", error.Message, StringComparison.Ordinal);
        Assert.Equal(5, error.Line);
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.6, 2.9: an external program must not contradict the caller's UNITS and WORKPLANE: ERROR on its block, in
    // its file.
    [Theory]
    [InlineData("UNITS=INCH")]
    [InlineData("WORKPLANE=ZX")]
    public void CallExternal_ContradictingTheCaller_IsAnErrorInTheCalledFile(string word)
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External(word, "RAPID IX=10"))
            .Run(s_caller);

        Diagnostic error = vm.Single(DiagnosticCodes.ExternalProgramContradictsCaller);
        Assert.Equal("O9010.ncx", error.File);
        Assert.Equal(3, error.Line);
        Assert.Equal(0m, vm.Position("X").Value);
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.6: UNITS and WORKPLANE that agree with the caller's contradict nothing.
    [Fact]
    public void CallExternal_UnitsAndWorkplaneOfTheCaller_Run()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("UNITS=MM WORKPLANE=XY", "RAPID IX=10"))
            .Run(s_caller);

        vm.AssertNoDiagnostics();
        Assert.Equal(11m, vm.Position("X").Value);
    }

    // VM 2.9: a diagnostic on a block of a called program carries the file name of that program.
    [Fact]
    public void CallExternal_DiagnosticOnItsBlock_CarriesItsFileName()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("LINE IX=10 F=100"))
            .Run(s_caller);

        vm.AssertNoErrors();
        Diagnostic warning = vm.Single(DiagnosticCodes.SpindleOffBeforeLine);
        Assert.Equal("O9010.ncx", warning.File);
        Assert.Equal(3, warning.Line);
    }

    // VM 2.9: what reading the external program found carries its file name, and an ERROR there stops the run.
    [Fact]
    public void CallExternal_ProgramWithAnErrorOfTheParser_StopsTheRunWithItsDiagnostic()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("LINE:X=1"))
            .Run(s_caller);

        Diagnostic error = Assert.Single(vm.Diagnostics.Items);
        Assert.Equal("O9010.ncx", error.File);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.6: CALL assigns its ARG words to the callee's locals for an external program as well, and VAR_CHANGE
    // reports them at the CALL block (virtual machine 7).
    [Fact]
    public void CallExternal_ArgWords_AreTheLocalsOfTheProgram()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("VAR:Q1={$V1 * 2}"))
            .Run(VmHarness.File("CALL=\"O9010\" ARG:V1=3", "PROGRAM=END"));

        vm.AssertNoDiagnostics();
        Assert.Equal("6", vm.Var("Q1"));
        Assert.Null(vm.Var("V1"));
        Assert.Equal(["VAR_CHANGE(3): V1 ? -> 3", "VAR_CHANGE(3): Q1 ? -> 6"], vm.Events.LinesOf("VAR_CHANGE"));
    }

    // Language 4.13: a subprogram belongs to its file; a CALL in the external program enters the subprogram of that
    // file, not the one of the caller's file with the same name.
    [Fact]
    public void CallExternal_CallInsideIt_EntersTheSubprogramOfItsOwnFile()
    {
        string external = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="O9010"
            CALL=20
            PROGRAM=END
            SUB=BEGIN NAME=20
            VAR:Q1=2
            SUB=END
            FILE=END
            """;

        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", external)
            .Run(VmHarness.File("CALL=\"O9010\"", "PROGRAM=END", "SUB=BEGIN NAME=20", "VAR:Q1=1", "SUB=END"));

        vm.AssertNoErrors();
        Assert.Equal("2", vm.Var("Q1"));
    }

    // VM 3.6: JUMP=END in the external program continues at its PROGRAM=END, which returns to the caller.
    [Fact]
    public void CallExternal_JumpEndInsideIt_ReturnsToTheCaller()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("JUMP=END", "VAR:Q2=1"))
            .Run(s_caller);

        vm.AssertNoErrors();
        Assert.Null(vm.Var("Q2"));
        Assert.Equal(1m, vm.Position("X").Value);
        Assert.Equal("O9010.ncx", vm.Single(DiagnosticCodes.UnreachableBlock).File);
    }

    // VM 3.6, 5: an external program is read, and the rules about its blocks as they are written reported, once per
    // run, however often a CALL enters it.
    [Fact]
    public void CallExternal_EnteredTwice_ReportsItsPrePassOnce()
    {
        InterpretedHarness vm = new InterpretedHarness()
            .WithExternalProgram("O9010", External("RAPID IX=10", "JUMP=END", "VAR:Q2=1"))
            .Run(VmHarness.File("UNITS=MM", "RAPID X=0 Y=0 Z=0", "CALL=\"O9010\" TIMES=2", "PROGRAM=END"));

        vm.AssertNoErrors();
        Assert.Equal([DiagnosticCodes.UnreachableBlock], vm.Codes());
        Assert.Equal(20m, vm.Position("X").Value);
    }

    // An external program O9010 with these lines between its PROGRAM=BEGIN, on line 2, and its PROGRAM=END.
    private static string External(params string[] lines)
    {
        return "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"O9010\"\n" + string.Join("\n", lines)
            + "\nPROGRAM=END\nFILE=END\n";
    }
}
