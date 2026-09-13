using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// A virtual machine with a FakeListener subscribed before the first block: a whole file run in STATIC mode, or blocks
/// executed one by one as the lines of a user file.
/// </summary>
internal static class EventRuns
{
    /// <summary>
    /// Runs a whole file in STATIC mode against the built-in default machine unless the test names another, and
    /// asserts that no ERROR stopped it.
    /// </summary>
    public static FakeListener Run(string text, MachineConfig? machine = null, VmOptions? options = null)
    {
        MachineConfig runMachine = machine ?? VmMachines.Default();
        var diagnostics = new Diagnostics("test.ncx");
        var vm = new Ncx.Core.VirtualMachine.VirtualMachine(runMachine,
            options ?? VmOptions.ForMachine(runMachine), diagnostics);
        var listener = new FakeListener();
        vm.Subscribe(listener);

        NcxProgram program = Parser.Parse(text, "test.ncx", new ParserOptions());
        vm.Run(program);

        Assert.False(diagnostics.HasErrors, program.Diagnostics.ToText() + diagnostics.ToText());
        return listener;
    }

    /// <summary>
    /// Executes each line as the next block of a user file against the built-in default machine.
    /// </summary>
    public static FakeListener Execute(params string[] lines)
    {
        return Execute(VmMachines.Default(), null, lines);
    }

    /// <summary>
    /// Executes each line as the next block of a user file against a machine, with the options the machine gives
    /// unless the test names its own.
    /// </summary>
    public static FakeListener Execute(MachineConfig machine, VmOptions? options, params string[] lines)
    {
        var harness = new VmHarness(machine, options);
        var listener = new FakeListener();
        harness.Vm.Subscribe(listener);
        harness.Execute(lines);
        return listener;
    }
}
