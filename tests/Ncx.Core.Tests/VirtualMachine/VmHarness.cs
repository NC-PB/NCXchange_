using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// A virtual machine for the tests: blocks executed one by one as the lines of a user file, or a whole file run in
/// STATIC mode, with the state and the diagnostics to assert on.
/// </summary>
internal sealed class VmHarness
{
    private int _line;

    /// <summary>
    /// A virtual machine against a machine, with the options the machine gives unless the test names its own.
    /// </summary>
    public VmHarness(MachineConfig machine, VmOptions? options = null)
    {
        Diagnostics = new Diagnostics("test.ncx");
        Vm = new Ncx.Core.VirtualMachine.VirtualMachine(machine, options ?? VmOptions.ForMachine(machine),
            Diagnostics);
    }

    public Ncx.Core.VirtualMachine.VirtualMachine Vm { get; }

    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// The result of <see cref="Run"/>; null for blocks executed one by one.
    /// </summary>
    public RunResult? Result { get; private set; }

    public ChannelState State => Vm.State;

    /// <summary>
    /// Runs a whole file in STATIC mode.
    /// </summary>
    public static VmHarness Run(string text, MachineConfig machine, VmOptions? options = null)
    {
        var harness = new VmHarness(machine, options);
        NcxProgram program = Parser.Parse(text, "test.ncx", new ParserOptions());
        harness.Result = harness.Vm.Run(program);
        return harness;
    }

    /// <summary>
    /// A file of one program named TEST with these lines between PROGRAM=BEGIN and PROGRAM=END, and the subprogram
    /// lines after it.
    /// </summary>
    public static string File(params string[] lines)
    {
        return "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"TEST\"\n" + string.Join("\n", lines) + "\nFILE=END\n";
    }

    /// <summary>
    /// Executes each line as the next block of a user file; the line must parse without a diagnostic.
    /// </summary>
    public VmHarness Execute(params string[] lines)
    {
        foreach (string line in lines)
        {
            _line++;
            var parsing = new Diagnostics("test.ncx");
            Block? block = Parser.ParseBlock(line, _line, new ParserOptions(), parsing);
            Assert.True(parsing.Items.Count == 0, parsing.ToText());
            Assert.NotNull(block);
            Vm.Execute(block);
        }

        return this;
    }

    /// <summary>
    /// How many diagnostics carry this code.
    /// </summary>
    public int Count(string code)
    {
        int count = 0;
        foreach (Diagnostic diagnostic in Diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public List<string> Codes()
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in Diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// The messages of the diagnostics with this code.
    /// </summary>
    public List<string> Messages(string code)
    {
        var messages = new List<string>();
        foreach (Diagnostic diagnostic in Diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                messages.Add(diagnostic.Message);
            }
        }

        return messages;
    }

    /// <summary>
    /// Asserts that nothing was reported, printing the list otherwise.
    /// </summary>
    public void AssertNoDiagnostics()
    {
        Assert.True(Diagnostics.Items.Count == 0, Diagnostics.ToText());
    }

    public AxisPosition Position(string axis)
    {
        return State.Motion.Position[axis];
    }

    /// <summary>
    /// Puts an axis at a known position before the blocks of a test.
    /// </summary>
    public VmHarness At(string axis, decimal value, PositionFrame frame)
    {
        State.Motion.Position[axis] = new AxisPosition(value, frame, Known: true);
        return this;
    }
}
