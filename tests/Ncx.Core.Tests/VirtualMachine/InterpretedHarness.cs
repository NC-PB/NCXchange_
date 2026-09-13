using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Tests.VirtualMachine.Events;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// A virtual machine in INTERPRETED mode for the tests (virtual machine 1, 3.6): a whole file run with its flow, with a
/// FakeListener subscribed before the first block, the state and the diagnostics to assert on, and the external
/// programs a CALL may load as texts by their file names.
/// </summary>
internal sealed class InterpretedHarness
{
    // The external programs by the name a CALL gives, as texts.
    private readonly Dictionary<string, string> _externalPrograms = new(StringComparer.Ordinal);

    /// <summary>
    /// A virtual machine in INTERPRETED mode against a machine, the built-in default machine of D103 unless the test
    /// names another, with the options the machine gives unless the test names its own.
    /// </summary>
    /// <param name="machine">The machine; null for the built-in default machine.</param>
    /// <param name="options">The options of the run; null for those the machine gives.</param>
    /// <param name="startValues">The start values of the vars file; null without one.</param>
    /// <param name="listen">False for a run without a listener, which raises no event and takes no snapshot.</param>
    public InterpretedHarness(MachineConfig? machine = null, VmOptions? options = null,
        IReadOnlyDictionary<string, Value>? startValues = null, bool listen = true)
    {
        MachineConfig runMachine = machine ?? VmMachines.Default();
        Diagnostics = new Diagnostics("test.ncx");
        Vm = new Ncx.Core.VirtualMachine.VirtualMachine(runMachine, options ?? VmOptions.ForMachine(runMachine),
            Diagnostics, ExecutionMode.Interpreted, startValues)
        {
            ExternalPrograms = LoadExternalProgram,
        };
        if (listen)
        {
            Vm.Subscribe(Events);
        }
    }

    public Ncx.Core.VirtualMachine.VirtualMachine Vm { get; }

    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// Every event of the run.
    /// </summary>
    public FakeListener Events { get; } = new();

    /// <summary>
    /// The result of the run; null before it.
    /// </summary>
    public RunResult? Result { get; private set; }

    /// <summary>
    /// What the parser reported on the file of the run.
    /// </summary>
    public Diagnostics? ParseDiagnostics { get; private set; }

    public ChannelState State => Vm.State;

    /// <summary>
    /// Runs a file in INTERPRETED mode against the built-in default machine and asserts that no ERROR stopped it.
    /// </summary>
    public static InterpretedHarness RunClean(string text)
    {
        InterpretedHarness harness = new InterpretedHarness().Run(text);
        harness.AssertNoErrors();
        return harness;
    }

    /// <summary>
    /// An external program that CALL="name" loads, as the text of its file (virtual machine 3.6).
    /// </summary>
    public InterpretedHarness WithExternalProgram(string name, string text)
    {
        _externalPrograms[name] = text;
        return this;
    }

    /// <summary>
    /// Parses a file, which must parse without an ERROR, and runs it in INTERPRETED mode.
    /// </summary>
    /// <param name="text">The file.</param>
    /// <param name="programName">The program to run; null for the first of the file.</param>
    public InterpretedHarness Run(string text, string? programName = null)
    {
        NcxProgram program = Parser.Parse(text, "test.ncx", new ParserOptions());
        ParseDiagnostics = program.Diagnostics;
        Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
        Result = Vm.RunInterpreted(program, programName);
        return this;
    }

    /// <summary>
    /// Asserts that no ERROR stopped the run, printing the diagnostics otherwise.
    /// </summary>
    public void AssertNoErrors()
    {
        Assert.False(Diagnostics.HasErrors, Diagnostics.ToText());
        Assert.False(Result?.Stopped ?? true, "The run did not end.");
    }

    /// <summary>
    /// Asserts that nothing was reported, printing the list otherwise.
    /// </summary>
    public void AssertNoDiagnostics()
    {
        Assert.True(Diagnostics.Items.Count == 0, Diagnostics.ToText());
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
    /// The one diagnostic with this code.
    /// </summary>
    public Diagnostic Single(string code)
    {
        var found = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in Diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                found.Add(diagnostic);
            }
        }

        return Assert.Single(found);
    }

    /// <summary>
    /// The value of a variable after the run as NCX writes it, UNKNOWN, or null for an unassigned variable.
    /// </summary>
    public string? Var(string name)
    {
        return State.Vars.Get(name)?.ToString();
    }

    public AxisPosition Position(string axis)
    {
        return State.Motion.Position[axis];
    }

    // The external program of CALL="name", parsed as the composition root parses it; null for a name the test gave
    // none.
    private NcxProgram? LoadExternalProgram(string name)
    {
        return _externalPrograms.TryGetValue(name, out string? text)
            ? Parser.Parse(text, name + ".ncx", new ParserOptions())
            : null;
    }
}
