using Ncx.Cli;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// The stages that check, trace and annotate share: read the input and the machine file, parse, expand, run STATIC,
/// collect the diagnostics (architecture 10; virtual machine 1; P1-07).
/// </summary>
public sealed class PipelineTests : IDisposable
{
    // A drilling cycle called at one position (language 4.7, virtual machine 3.3).
    private static readonly string s_drilling = CliHarness.OneProgram(
        "UNITS=MM",
        "TOOL=1 RPM=1000",
        "SPINDLE=CW",
        "RAPID X=0 Y=0 Z=10",
        "CYCLE=DRILL CLEARANCE=2 DEPTH=-10 CYCLE_F=100",
        "CYCLE_CALL X=10 Y=10",
        "CYCLE=OFF");

    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // D37, VM 3.3: with ExpandCycles the call is raised as its individual MOTION events; without it the CYCLE_CALL
    // block raises none.
    [Fact]
    public void Pipeline_ExpandCycles_RaisesTheMotionsOfTheCycleCall()
    {
        string file = _cli.WriteFile("drill.ncx", s_drilling);
        var expanded = new MotionRecorder();
        var plain = new MotionRecorder();

        PipelineRun expandedRun = Pipeline.Run(new RunSettings { File = file, ExpandCycles = true }, [expanded]);
        PipelineRun plainRun = Pipeline.Run(new RunSettings { File = file }, [plain]);

        Assert.False(expandedRun.Diagnostics.HasErrors, expandedRun.Diagnostics.ToText());
        Assert.False(plainRun.Diagnostics.HasErrors, plainRun.Diagnostics.ToText());
        Assert.True(expanded.MotionsOnLine(8) >= 3, $"{expanded.MotionsOnLine(8)} MOTION events at the CYCLE_CALL.");
        Assert.Equal(0, plain.MotionsOnLine(8));
    }

    // The diagnostics come in the order they were reported: the machine file, which is loaded before the run, then
    // the parser, the expander and the virtual machine (D98).
    [Fact]
    public void Pipeline_Diagnostics_TheMachineFileFirst()
    {
        string file = _cli.CopyExample("PATTERN_LOOP.ncx");
        string machine = _cli.WriteFile("clutch.toml", TestMachines.ClutchMillWithUnknownKey);

        PipelineRun run = Pipeline.Run(new RunSettings { File = file, MachineFile = machine }, []);

        Assert.Equal(2, run.Diagnostics.Items.Count);
        Assert.Equal(machine, run.Diagnostics.Items[0].File);
        Assert.Equal(file, run.Diagnostics.Items[1].File);
        Assert.Equal("VM540", run.Diagnostics.Items[1].Code);
    }

    // The program the virtual machine ran is the expanded program; there is none when the parser reports an ERROR,
    // which stops the run before its first block (VM 2.9).
    [Fact]
    public void Pipeline_ParserError_GivesNoProgram()
    {
        string good = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string bad = _cli.WriteFile("bad.ncx", CliHarness.OneProgram("LINE X=1 SPEED=5"));

        PipelineRun goodRun = Pipeline.Run(new RunSettings { File = good }, []);
        PipelineRun badRun = Pipeline.Run(new RunSettings { File = bad }, []);

        Assert.NotNull(goodRun.Program);
        Assert.True(goodRun.InputsRead);
        Assert.Null(badRun.Program);
        Assert.True(badRun.InputsRead);
        Assert.Equal(1, badRun.ExitCode(strict: false));
    }

    // An input that cannot be read decides exit code 2 before the run starts, and so does a machine file that cannot
    // be read; both are reported (D97).
    [Fact]
    public void Pipeline_InputAndMachineFileUnreadable_ReportsBothAndExitsTwo()
    {
        string file = _cli.PathOf("missing.ncx");
        string machine = _cli.PathOf("missing.toml");

        PipelineRun run = Pipeline.Run(new RunSettings { File = file, MachineFile = machine }, []);

        Assert.False(run.InputsRead);
        Assert.Null(run.Program);
        Assert.Equal(2, run.ExitCode(strict: false));
        Assert.Equal(["CLI002", "CLI100"], [run.Diagnostics.Items[0].Code, run.Diagnostics.Items[1].Code]);
    }

    // A listener that counts the MOTION events of each line (code-guidelines 8: a small hand-written fake).
    private sealed class MotionRecorder : IVmListener
    {
        private readonly Dictionary<int, int> _motionsByLine = [];

        public void On(VmEvent vmEvent)
        {
            if (vmEvent is MotionEvent motion)
            {
                _motionsByLine[motion.Block.Line] = MotionsOnLine(motion.Block.Line) + 1;
            }
        }

        public int MotionsOnLine(int line)
        {
            return _motionsByLine.TryGetValue(line, out int count) ? count : 0;
        }
    }
}
