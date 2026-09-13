using System.Text;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The event sequence of an example, recorded by a listener as one line per event, kind(line): payload, and compared
/// with its expected file as a whole (P1-05; virtual machine 7).
/// </summary>
public sealed class ExampleEventTests
{
    // P1-05, done when: a listener records the event sequence of 2.5D_FRAESEN.ncx, run STATIC without a machine file
    // against the built-in default machine of D103 as ncx check runs it, and it equals the expected text file.
    [Fact]
    public void Events_Fraesen25D_EqualTheExpectedFile()
    {
        string actual = Events("2.5D_FRAESEN.ncx");

        // The expected file is a file of the repository, read from the root found from the test assembly (tests/README).
        string expectedFile = Path.Combine(
            Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", "2.5D_FRAESEN.events.txt");
        string expected = File.ReadAllText(expectedFile).ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
    }

    // The events of an example, one line each, every line ended with LF.
    private static string Events(string example)
    {
        MachineConfig machine = DefaultMachine.Create();
        var diagnostics = new Diagnostics(example);
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine), diagnostics);
        var recorder = new EventRecorder();
        vm.Subscribe(recorder);

        NcxProgram program = Parser.Parse(Fixture.ReadText(example), example, new ParserOptions());
        vm.Run(program);

        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
        return recorder.Text.ToString();
    }

    // A listener that writes every event as one line (code-guidelines 8: a small hand-written fake).
    private sealed class EventRecorder : IVmListener
    {
        public StringBuilder Text { get; } = new();

        public void On(VmEvent vmEvent)
        {
            Text.Append(vmEvent.ToString()).Append('\n');
        }
    }
}
